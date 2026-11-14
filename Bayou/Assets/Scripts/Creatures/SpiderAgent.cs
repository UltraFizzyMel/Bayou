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
            "Beware the spider closing in, for its bite is worse than its bark. Stay off its web — it slows you and gives the spider a burst of speed.";

        public const string PoisonDeathTip =
            "The spider's venom kept eating away at you...  Break away, and don't stand in its web while it ticks.";

        [Header("Movement")]
        [SerializeField] private float passiveSpeed = 1.35f;
        [SerializeField] private float chaseSpeed = 3.3f;
        [SerializeField] private float turnSpeedDegrees = 220f;
        [SerializeField] private float wanderRadius = 4.5f;
        [SerializeField] private float leashRadius = 18f;

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
        private Animator _animator;
        private bool _snapToGround;
        private Vector3 _spawn;
        private Vector3 _wander;
        private float _nextWander;
        private float _stunUntil;
        private float _nextWeb;
        private float _webPlantedUntil;
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
            _animator = GetComponentInChildren<Animator>();
            if (_animator != null)
                _animator.applyRootMotion = false;
            var hasArt = HasArtBody();
            _snapToGround = !hasArt;
            // The placed red spider faces away from the porch. Sense a full circle
            // so walking up to it is enough to start the hunt.
            _sense.Configure(hasArt ? 12f : 6f, hasArt ? 18f : 12f, hasArt ? 360f : 140f);
            _sense.EnsurePlayer();
            _spawn = transform.position;
            _wander = _spawn;
            _health = maxHealth;
            EnsureBody();
        }

        private void Update()
        {
            if (_dead) return;
            if (CreatureAi.IsFrozen)
                return;

            if (_snapToGround)
                CreatureController.SnapBodyToGround(transform, 0.22f);

            if (Time.time < _stunUntil)
            {
                _windingUp = false;
                return;
            }

            if (Time.time < _webPlantedUntil)
                return;

            if (!_sense.TrySensePlayer(out var player))
            {
                if (!_sense.HasRecentSense || _sense.Player == null)
                {
                    _windingUp = false;
                    TickWander(Time.deltaTime);
                    SetAgro(false);
                    return;
                }
                SetAgro(true);
                player = _sense.Player;
            }

            var to = player.position - transform.position;
            to.y = 0f;
            var dist = to.magnitude;

            if (Flat(player.position - _spawn).magnitude > leashRadius)
            {
                _windingUp = false;
                SetAgro(false);
                var home = Flat(_spawn - transform.position);
                if (home.sqrMagnitude > 0.4f)
                    MoveToward(home.normalized, passiveSpeed, Time.deltaTime);
                else
                    TickWander(Time.deltaTime);
                return;
            }

            SetAgro(true);

            if (_windingUp)
            {
                Face(to, Time.deltaTime);
                PlayAnim("Bite_Attack");
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
                Face(to, Time.deltaTime);
                return;
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
            {
                PlayAnim("Hurt");
                return NetHitResult.Damaged;
            }

            _dead = true;
            gameObject.SetActive(false);
            PlayAnim("Die");
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
            PlayAnim("Web_Attack");
            // SpiderRig|Spider_Attack_Web is frames 0–48 at 24 fps (2s). Stay planted for the clip.
            _webPlantedUntil = Time.time + WebShotSeconds();
            var from = transform.position + Vector3.up * 0.35f;
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "SpiderWeb";
            go.transform.position = from;
            go.transform.localScale = Vector3.one * 0.22f;
            var web = go.AddComponent<SpiderWeb>();
            web.Launch(this, from, target, webShotSpeed);
        }

        private float WebShotSeconds()
        {
            const float fallback = 48f / 24f;
            if (_animator == null || _animator.runtimeAnimatorController == null)
                return fallback;

            var clips = _animator.runtimeAnimatorController.animationClips;
            for (var i = 0; i < clips.Length; i++)
            {
                var clip = clips[i];
                if (clip == null) continue;
                if (clip.name.IndexOf("Attack_Web", System.StringComparison.Ordinal) < 0) continue;
                return clip.length > 0.05f ? clip.length : fallback;
            }

            return fallback;
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

        private void Start()
        {
            FitCollider();
        }

        public bool IsHostile { get; private set; }

        private void SetAgro(bool value)
        {
            if (value && !IsHostile)
                CreatureAggro.Notify();
            IsHostile = value;
            if (_animator == null || _animator.runtimeAnimatorController == null) return;
            _animator.SetBool("IsAgro", value);
        }

        private void PlayAnim(string state)
        {
            if (_animator == null || _animator.runtimeAnimatorController == null) return;
            var trigger = state switch
            {
                "Bite_Attack" => "Bite",
                "Web_Attack" => "Web",
                "Hurt" => "Hurt",
                "Die" => "Die",
                _ => null
            };
            if (trigger != null)
                _animator.SetTrigger(trigger);
            else
                _animator.Play(state);
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

            if (HasArtBody() || transform.Find("SpiderMesh") != null) return;

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

        private bool HasArtBody()
        {
            if (_animator != null &&
                _animator.runtimeAnimatorController != null &&
                _animator.runtimeAnimatorController.name == "Spider")
                return true;
            return GetComponentInChildren<Renderer>(true) != null;
        }

        private void FitCollider()
        {
            var capsule = GetComponent<CapsuleCollider>();
            if (capsule == null) return;
            var renderers = GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0) return;

            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                    bounds.Encapsulate(renderers[i].bounds);
            }

            if (bounds.size.y > 6f || bounds.size.x > 8f || bounds.size.z > 8f)
                return;

            capsule.isTrigger = true;
            capsule.height = Mathf.Max(0.55f, bounds.size.y);
            capsule.radius = Mathf.Clamp(Mathf.Max(bounds.extents.x, bounds.extents.z), 0.35f, 1.5f);
            capsule.center = transform.InverseTransformPoint(bounds.center);
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
