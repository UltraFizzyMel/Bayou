using Bayou.Player;
using UnityEngine;

namespace Bayou.Creatures
{
    public enum SpiderAttack
    {
        None,
        Bite,
        Poison
    }

    /// <summary>
    /// First spider. Slow on open ground, faster on its own web.
    /// Shoots a limited number of slowing webs, then closes in for a poison bite.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CreatureSense))]
    public sealed class SpiderAgent : MonoBehaviour, INetHittable
    {
        public const string BiteDeathTip =
            "The spider bites when it gets close. Stay off the web — you are slower there, and the spider is faster.";

        public const string PoisonDeathTip =
            "The venom kept working after the bite. Break away, and don't stand in the web while it ticks.";

        [Header("Movement")]
        [SerializeField] private float passiveSpeed = 1.35f;
        [SerializeField] private float chaseSpeed = 3.3f;
        [SerializeField] private float turnSpeedDegrees = 220f;
        [SerializeField] private float wanderRadius = 4.5f;

        [Header("Web")]
        [SerializeField] private int maxWebs = 3;
        [SerializeField] private float webMinRange = 2.8f;
        [SerializeField] private float webMaxRange = 8.5f;
        [SerializeField] private float webCooldown = 2.6f;
        [SerializeField] private float webShotSpeed = 11f;

        [Header("Bite")]
        [SerializeField] private float biteRange = 1.45f;
        [SerializeField] private float biteWindup = 0.36f;
        [SerializeField] private float biteCooldown = 1.7f;
        [SerializeField] private float biteDamage = 1f;
        [SerializeField] private float venomPerSecond = 0.55f;
        [SerializeField] private float venomSeconds = 4.5f;

        [Header("Health")]
        [SerializeField] private float maxHealth = 4f;
        [SerializeField] private float hitStunSeconds = 0.28f;

        private CreatureSense _sense;
        private Vector3 _spawn;
        private Vector3 _wander;
        private float _nextWander;
        private float _stunUntil;
        private float _nextWeb;
        private float _nextBite;
        private float _windupUntil;
        private bool _windingUp;
        private float _health;
        private bool _dead;
        private Vector3 _moveDir = Vector3.forward;
        private SpiderAttack _lastAttack = SpiderAttack.None;

        public bool IsNetHittable => !_dead && _health > 0f;
        public SpiderAttack LastAttack => _lastAttack;
        public string DeathTip => _lastAttack == SpiderAttack.Poison ? PoisonDeathTip : BiteDeathTip;

        private void Awake()
        {
            _sense = GetComponent<CreatureSense>();
            _sense.EnsurePlayer();
            _spawn = transform.position;
            _wander = _spawn;
            _health = maxHealth;
            EnsureBody();
        }

        private void Update()
        {
            if (_dead) return;
            if (Bayou.GameplayPause.BlocksWorldInteract && Time.timeScale <= 0f)
                return;

            CreatureController.SnapBodyToGround(transform, 0.22f);

            if (Time.time < _stunUntil)
            {
                _windingUp = false;
                return;
            }

            if (!_sense.TrySensePlayer(out var player))
            {
                if (!_sense.HasRecentSense || _sense.Player == null)
                {
                    _windingUp = false;
                    TickWander(Time.deltaTime);
                    return;
                }

                player = _sense.Player;
            }

            var to = player.position - transform.position;
            to.y = 0f;
            var dist = to.magnitude;

            if (_windingUp)
            {
                Face(to, Time.deltaTime);
                if (Time.time < _windupUntil) return;
                _windingUp = false;
                _nextBite = Time.time + biteCooldown;
                if (dist <= biteRange + 0.4f)
                    LandBite(player, to);
                return;
            }

            if (dist <= biteRange && Time.time >= _nextBite)
            {
                _windingUp = true;
                _windupUntil = Time.time + biteWindup;
                _lastAttack = SpiderAttack.Bite;
                Face(to, Time.deltaTime);
                return;
            }

            if (dist >= webMinRange && dist <= webMaxRange && Time.time >= _nextWeb &&
                SpiderWeb.CountFor(this) < Mathf.Max(1, maxWebs))
            {
                ShootWeb(player.position);
                _nextWeb = Time.time + webCooldown;
            }

            var speed = (dist <= webMaxRange ? chaseSpeed : passiveSpeed) * SpiderWeb.SpeedBonusFor(this);
            if (to.sqrMagnitude > 0.0001f)
                MoveToward(to.normalized, speed, Time.deltaTime);
        }

        public NetHitResult OnNetHit(NetHitInfo info)
        {
            if (!IsNetHittable)
                return NetHitResult.Ignored;

            if (!info.IsMelee)
            {
                _stunUntil = Time.time + 1.15f;
                _windingUp = false;
                return NetHitResult.Stunned;
            }

            var amount = info.Damage > 0f ? info.Damage : 1f;
            _health -= amount;
            _stunUntil = Time.time + hitStunSeconds;
            if (_health > 0f)
                return NetHitResult.Damaged;

            _dead = true;
            gameObject.SetActive(false);
            return NetHitResult.Killed;
        }

        private void LandBite(Transform player, Vector3 to)
        {
            _lastAttack = SpiderAttack.Bite;
            if (to.sqrMagnitude > 0.0001f)
                transform.position += to.normalized * 0.35f;

            var receiver = player.GetComponent<IPlayerHurtReceiver>() ??
                           player.GetComponentInParent<IPlayerHurtReceiver>();
            if (receiver != null)
            {
                var away = player.position - transform.position;
                away.y = 0f;
                if (away.sqrMagnitude < 0.001f)
                    away = transform.forward;
                receiver.OnCreatureHit(new CreatureHitInfo(biteDamage, away.normalized * 5.5f, gameObject));
            }

            var health = player.GetComponent<PlayerHealth>() ?? player.GetComponentInParent<PlayerHealth>();
            if (health != null && health.IsAlive)
            {
                _lastAttack = SpiderAttack.Poison;
                health.ApplyVenom(venomPerSecond, venomSeconds, PoisonDeathTip);
            }
        }

        private void ShootWeb(Vector3 target)
        {
            var from = transform.position + Vector3.up * 0.35f;
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "SpiderWeb";
            go.transform.position = from;
            go.transform.localScale = Vector3.one * 0.22f;
            var web = go.AddComponent<SpiderWeb>();
            web.Launch(this, from, target, webShotSpeed);
        }

        private void TickWander(float dt)
        {
            if (Time.time >= _nextWander || Flat(_wander - transform.position).sqrMagnitude < 0.2f)
            {
                var ring = Random.insideUnitCircle * wanderRadius;
                _wander = _spawn + new Vector3(ring.x, 0f, ring.y);
                _nextWander = Time.time + Random.Range(1.4f, 2.8f);
            }

            var to = Flat(_wander - transform.position);
            if (to.sqrMagnitude < 0.04f) return;
            MoveToward(to.normalized, passiveSpeed * SpiderWeb.SpeedBonusFor(this), dt);
        }

        private void MoveToward(Vector3 dir, float speed, float dt)
        {
            Face(dir, dt);
            transform.position += _moveDir * speed * dt;
        }

        private void Face(Vector3 dir, float dt)
        {
            if (dir.sqrMagnitude < 0.0001f) return;
            dir.Normalize();
            _moveDir = Vector3.RotateTowards(
                _moveDir.sqrMagnitude < 0.0001f ? dir : _moveDir,
                dir,
                Mathf.Deg2Rad * turnSpeedDegrees * dt,
                0f);
            if (_moveDir.sqrMagnitude < 0.0001f) return;
            var look = Quaternion.LookRotation(_moveDir, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, look, turnSpeedDegrees * dt);
        }

        private void EnsureBody()
        {
            if (GetComponent<Collider>() == null)
            {
                var capsule = gameObject.AddComponent<CapsuleCollider>();
                capsule.isTrigger = true;
                capsule.height = 0.7f;
                capsule.radius = 0.45f;
                capsule.center = new Vector3(0f, 0.2f, 0f);
            }

            if (transform.Find("SpiderMesh") != null) return;

            var root = new GameObject("SpiderMesh");
            root.transform.SetParent(transform, false);
            var mat = Bayou.Rendering.BayouShaderUtil.CreateUnlitColor(new Color(0.16f, 0.1f, 0.08f, 1f));

            var body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localScale = new Vector3(0.55f, 0.32f, 0.7f);
            StripCollider(body);
            Tint(body, mat);

            var abdomen = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            abdomen.name = "Abdomen";
            abdomen.transform.SetParent(root.transform, false);
            abdomen.transform.localPosition = new Vector3(0f, 0.05f, -0.38f);
            abdomen.transform.localScale = new Vector3(0.48f, 0.36f, 0.55f);
            StripCollider(abdomen);
            Tint(abdomen, mat);

            for (var i = 0; i < 4; i++)
            {
                var side = i < 2 ? -1f : 1f;
                var z = i % 2 == 0 ? 0.12f : -0.18f;
                var leg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                leg.name = "Leg" + i;
                leg.transform.SetParent(root.transform, false);
                leg.transform.localPosition = new Vector3(side * 0.42f, 0.02f, z);
                leg.transform.localRotation = Quaternion.Euler(0f, 0f, side * -28f);
                leg.transform.localScale = new Vector3(0.62f, 0.05f, 0.06f);
                StripCollider(leg);
                Tint(leg, mat);
            }
        }

        private static void StripCollider(GameObject go)
        {
            var col = go.GetComponent<Collider>();
            if (col != null)
                Destroy(col);
        }

        private static void Tint(GameObject go, Material mat)
        {
            var rend = go.GetComponent<Renderer>();
            if (rend != null)
                rend.sharedMaterial = mat;
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }
    }
}
