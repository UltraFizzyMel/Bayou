using System.Collections.Generic;
using Bayou.Combat;
using Bayou.Inventory;
using UnityEngine;

namespace Bayou.Creatures
{
    public enum CreatureMode
    {
        Passive,
        Active
    }

    public enum CreatureNetBehavior
    {
        /// <summary>Type 1 — snake: net catch → inventory.</summary>
        CatchOnNet,
        /// <summary>Type 2 — crocodile: net temporarily stuns, then resumes chase.</summary>
        StunOnNet
    }

    /// <summary>
    /// Passive patrol/wander vs Active chase. Sensing via <see cref="CreatureSense"/>.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CreatureSense))]
    public sealed class CreatureController : MonoBehaviour, INetHittable
    {
        [Header("Identity")]
        [SerializeField] private CreatureNetBehavior netBehavior = CreatureNetBehavior.CatchOnNet;
        [SerializeField] private ItemDefinition inventoryItemWhenCaught;

        [Header("Movement")]
        [SerializeField] private float passiveSpeed = 1.4f;
        [SerializeField] private float activeSpeed = 3.2f;
        [SerializeField] private float turnSpeedDegrees = 140f;
        [SerializeField] private float arriveDistance = 0.35f;

        [Header("Type 1 — waypoint patrol (snake)")]
        [Tooltip("World-space waypoints. Use 2–4 (or more). Empty = stand still while Passive.")]
        [SerializeField] private Transform[] patrolWaypoints;
        [SerializeField] private bool loopPatrol = true;
        [SerializeField] private float waypointWaitSeconds = 0.4f;

        [Header("Type 2 — area wander (crocodile)")]
        [SerializeField] private AreaBounds wanderArea;
        [SerializeField] private float wanderRetargetSeconds = 2.5f;

        [Header("Net stun (croc)")]
        [SerializeField] private float stunSeconds = 2.5f;

        [Header("Health")]
        [SerializeField] private float maxHealth = 3f;
        [SerializeField] private float meleeHitDamage = 1f;
        [SerializeField] private float hitStunSeconds = 0.35f;
        [SerializeField] private float hitInvulnSeconds = 0.25f;
        [SerializeField] private float hitKnockback = 2.4f;
        [SerializeField] private float hitKnockbackSpeed = 9f;

        [Header("Chase")]
        [SerializeField] private float stopChaseDistance = 0.9f;

        private static readonly List<CreatureController> All = new();

        private CreatureSense _sense;
        private CreatureMode _mode = CreatureMode.Passive;
        private int _patrolIndex;
        private float _waitUntil;
        private Vector3 _wanderTarget;
        private float _nextWanderPick;
        private float _stunUntil;
        private float _invulnUntil;
        private bool _caught;
        private bool _dead;
        private float _health;
        private Vector3 _moveDir = Vector3.forward;
        private CreaturePlaceholderVisual _visual;
        private Vector3 _knockVel;
        private float _hudUntil;
        private bool _dying;
        private float _dieAt;
        private float _hpFlashUntil;

        public CreatureMode Mode => _mode;
        public CreatureNetBehavior NetBehavior => netBehavior;
        public bool IsActive => _mode == CreatureMode.Active;
        public bool IsStunned => Time.time < _stunUntil;
        public bool IsCaught => _caught;
        public bool IsDead => _dead;
        public bool IsAlive => !_caught && !_dead && _health > 0f;
        public float Health => _health;
        public float MaxHealth => maxHealth;
        public bool IsNetHittable => IsAlive;
        public static IReadOnlyList<CreatureController> Living => All;

        private void OnEnable()
        {
            if (!All.Contains(this))
                All.Add(this);
        }

        private void OnDisable()
        {
            All.Remove(this);
        }

        private void Awake()
        {
            _sense = GetComponent<CreatureSense>();
            _sense.EnsurePlayer();
            _visual = GetComponent<CreaturePlaceholderVisual>();
            if (_visual == null)
                _visual = gameObject.AddComponent<CreaturePlaceholderVisual>();
            _visual.Configure(
                netBehavior == CreatureNetBehavior.StunOnNet
                    ? new Color(0.22f, 0.42f, 0.22f, 1f)
                    : new Color(0.42f, 0.86f, 0.28f, 1f),
                crocodileShape: netBehavior == CreatureNetBehavior.StunOnNet);
            if (netBehavior == CreatureNetBehavior.StunOnNet && maxHealth <= 3.01f)
                maxHealth = 8f;
            _health = Mathf.Max(1f, maxHealth);
            _visual?.NotifyHealth(_health, maxHealth);
            if (wanderArea != null)
                _wanderTarget = wanderArea.RandomPointInside();
            else
                _wanderTarget = transform.position;

            if (patrolWaypoints != null && patrolWaypoints.Length > 0 && patrolWaypoints[0] != null)
                _patrolIndex = 0;
        }

        private void Start()
        {
            SnapBodyToGround();
        }

        private void Update()
        {
            if (_caught) return;

            if (_dying)
            {
                if (Time.time >= _dieAt)
                    FinishDeath();
                return;
            }

            ApplyKnockback(Time.deltaTime);
            _visual?.SetStunned(IsStunned);

            if (_dead) return;

            if (IsStunned)
                return;

            var sensed = _sense.TrySensePlayer(out var player);
            if (sensed || _sense.HasRecentSense)
                _mode = CreatureMode.Active;
            else
                _mode = CreatureMode.Passive;

            var dt = Time.deltaTime;
            if (_mode == CreatureMode.Active && _sense.Player != null)
                TickActive(_sense.Player, dt);
            else
                TickPassive(dt);
        }

        private void TickPassive(float dt)
        {
            if (netBehavior == CreatureNetBehavior.CatchOnNet)
                TickPatrol(dt);
            else
                TickWander(dt);
        }

        private void TickActive(Transform player, float dt)
        {
            var to = Flat(player.position - transform.position);
            var dist = to.magnitude;
            if (dist <= stopChaseDistance)
            {
                Face(to, dt);
                return;
            }

            if (to.sqrMagnitude > 0.0001f)
                MoveToward(to.normalized, activeSpeed, dt);

            // Keep crocs roughly in / near their area while chasing.
            if (netBehavior == CreatureNetBehavior.StunOnNet && wanderArea != null)
            {
                var clamped = wanderArea.ClampInside(transform.position);
                // Allow slight leash break while chasing, then soft pull.
                var leash = wanderArea.Center;
                var fromCenter = Flat(transform.position - leash);
                var maxLeash = EstimateAreaRadius() * 1.35f;
                if (fromCenter.magnitude > maxLeash)
                {
                    transform.position = Vector3.MoveTowards(
                        transform.position,
                        new Vector3(clamped.x, transform.position.y, clamped.z),
                        activeSpeed * dt);
                }
            }
        }

        private void TickPatrol(float dt)
        {
            if (patrolWaypoints == null || patrolWaypoints.Length == 0)
                return;

            if (Time.time < _waitUntil)
                return;

            var wp = patrolWaypoints[_patrolIndex];
            if (wp == null)
            {
                AdvancePatrol();
                return;
            }

            var to = Flat(wp.position - transform.position);
            if (to.magnitude <= arriveDistance)
            {
                AdvancePatrol();
                _waitUntil = Time.time + waypointWaitSeconds;
                return;
            }

            MoveToward(to.normalized, passiveSpeed, dt);
        }

        private void AdvancePatrol()
        {
            if (patrolWaypoints == null || patrolWaypoints.Length == 0) return;
            if (loopPatrol)
                _patrolIndex = (_patrolIndex + 1) % patrolWaypoints.Length;
            else
                _patrolIndex = Mathf.Min(_patrolIndex + 1, patrolWaypoints.Length - 1);
        }

        private void TickWander(float dt)
        {
            if (wanderArea == null)
            {
                // Fallback: small local wander around spawn.
                if (Time.time >= _nextWanderPick)
                {
                    _wanderTarget = transform.position + Random.insideUnitSphere * 3f;
                    _wanderTarget.y = transform.position.y;
                    _nextWanderPick = Time.time + wanderRetargetSeconds;
                }
            }
            else if (Time.time >= _nextWanderPick ||
                     Flat(_wanderTarget - transform.position).magnitude <= arriveDistance)
            {
                _wanderTarget = wanderArea.RandomPointInside();
                _nextWanderPick = Time.time + Random.Range(
                    wanderRetargetSeconds * 0.7f, wanderRetargetSeconds * 1.3f);
            }

            var to = Flat(_wanderTarget - transform.position);
            if (to.sqrMagnitude > 0.0001f)
                MoveToward(to.normalized, passiveSpeed, dt);

            if (wanderArea != null)
            {
                var p = transform.position;
                var c = wanderArea.ClampInside(p);
                p.x = c.x;
                p.z = c.z;
                transform.position = p;
            }
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
            if (_moveDir.sqrMagnitude > 0.0001f)
            {
                var look = Quaternion.LookRotation(_moveDir, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation, look, turnSpeedDegrees * dt);
            }
        }

        private float EstimateAreaRadius()
        {
            if (wanderArea == null) return 8f;
            // Approximate from a sample clamp — AreaBounds doesn't expose radius; use distance to edge sample.
            var center = wanderArea.Center;
            var edge = wanderArea.ClampInside(center + Vector3.forward * 100f);
            return Mathf.Max(2f, Flat(edge - center).magnitude);
        }

        private void ApplyKnockback(float dt)
        {
            if (_knockVel.sqrMagnitude < 0.0001f) return;
            transform.position += _knockVel * dt;
            _knockVel = Vector3.Lerp(_knockVel, Vector3.zero, 1f - Mathf.Exp(-10f * dt));
            if (_knockVel.sqrMagnitude < 0.04f)
                _knockVel = Vector3.zero;
        }

        private void OnGUI()
        {
            if (!IsAlive && !_dying) return;
            var show = _health < maxHealth - 0.01f || Time.time < _hudUntil || _mode == CreatureMode.Active;
            if (!show) return;

            var cam = Camera.main;
            if (cam == null) return;
            var world = transform.position + Vector3.up * 1.45f;
            var screen = cam.WorldToScreenPoint(world);
            if (screen.z <= 0.1f) return;

            const float width = 56f;
            const float height = 8f;
            var x = screen.x - width * 0.5f;
            var y = Screen.height - screen.y - height;
            var fill = Mathf.Clamp01(_health / Mathf.Max(0.01f, maxHealth));
            GUI.color = new Color(0f, 0f, 0f, 0.75f);
            GUI.DrawTexture(new Rect(x - 1f, y - 1f, width + 2f, height + 2f), Texture2D.whiteTexture);
            GUI.color = new Color(0.18f, 0.06f, 0.06f, 0.95f);
            GUI.DrawTexture(new Rect(x, y, width, height), Texture2D.whiteTexture);
            var fillColor = Time.time < _hpFlashUntil
                ? new Color(1f, 1f, 0.85f, 1f)
                : Color.Lerp(new Color(0.85f, 0.2f, 0.15f), new Color(0.35f, 0.85f, 0.3f), fill);
            GUI.color = fillColor;
            GUI.DrawTexture(new Rect(x, y, width * fill, height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        public NetHitResult OnNetHit(NetHitInfo info)
        {
            if (!IsAlive)
                return NetHitResult.Ignored;

            if (info.IsMelee)
            {
                if (Time.time < _invulnUntil)
                    return NetHitResult.Ignored;

                var amount = info.Damage > 0f ? info.Damage : meleeHitDamage;
                var killed = ApplyDamage(amount, info.HitPoint);
                var result = killed ? NetHitResult.Killed : NetHitResult.Damaged;
                CombatFeedback.PlayHit(transform.position, result, transform.position - info.HitPoint);
                if (killed)
                    Bayou.Audio.FishingAudio.Resolve()?.PlayMeleeKill();
                return result;
            }

            if (IsStunned)
                return NetHitResult.Ignored;

            if (netBehavior == CreatureNetBehavior.CatchOnNet)
            {
                CombatFeedback.PlayHit(transform.position, NetHitResult.Caught, Vector3.up);
                Catch();
                return NetHitResult.Caught;
            }

            _stunUntil = Time.time + Mathf.Max(0.2f, stunSeconds);
            _mode = CreatureMode.Active;
            _hudUntil = Time.time + stunSeconds + 1f;
            _visual?.FlashHurt();
            _visual?.SetStunned(true);
            CombatFeedback.PlayHit(transform.position, NetHitResult.Stunned, Vector3.up);
            return NetHitResult.Stunned;
        }

        public bool TryTakeDamage(float amount, Vector3 hitPoint)
        {
            return ApplyDamage(amount, hitPoint);
        }

        private bool ApplyDamage(float amount, Vector3 hitPoint)
        {
            if (!IsAlive || amount <= 0f)
                return false;
            if (Time.time < _invulnUntil)
                return false;

            _health = Mathf.Max(0f, _health - amount);
            _invulnUntil = Time.time + Mathf.Max(0.05f, hitInvulnSeconds);
            _stunUntil = Mathf.Max(_stunUntil, Time.time + Mathf.Max(0.05f, hitStunSeconds));
            _mode = CreatureMode.Active;
            _hudUntil = Time.time + 3.2f;
            _hpFlashUntil = Time.time + 0.16f;

            var away = transform.position - hitPoint;
            away.y = 0f;
            if (away.sqrMagnitude < 0.001f)
                away = -transform.forward;
            _knockVel = away.normalized * Mathf.Max(hitKnockbackSpeed, hitKnockback * 4f);

            _visual?.FlashHurt();
            _visual?.NotifyHealth(_health, maxHealth);

            if (_health > 0f)
                return false;

            Die();
            return true;
        }

        private void Die()
        {
            if (_dead || _dying) return;
            _dead = true;
            _dying = true;
            _health = 0f;
            _mode = CreatureMode.Passive;
            _dieAt = Time.time + 0.28f;
            _visual?.NotifyHealth(0f, maxHealth);
            _visual?.PlayDeath();
        }

        private void FinishDeath()
        {
            _dying = false;
            if (netBehavior == CreatureNetBehavior.CatchOnNet)
            {
                Catch();
                return;
            }

            gameObject.SetActive(false);
        }

        public void Catch()
        {
            if (_caught) return;
            _caught = true;
            _dead = true;
            _dying = false;
            _health = 0f;
            _mode = CreatureMode.Passive;
            Bayou.Audio.FishingAudio.Resolve()?.PlaySnagCatch();
            gameObject.SetActive(false);

            if (inventoryItemWhenCaught == null)
            {
                Debug.LogWarning($"[Creature] {name} caught but has no inventoryItemWhenCaught.");
                return;
            }

            CaughtFishPresenter.Present(inventoryItemWhenCaught);
        }

        public void ConfigureCatchItem(ItemDefinition item) => inventoryItemWhenCaught = item;

        public void ConfigureAsSnake(Transform[] waypoints, ItemDefinition item)
        {
            netBehavior = CreatureNetBehavior.CatchOnNet;
            patrolWaypoints = waypoints;
            inventoryItemWhenCaught = item;
            maxHealth = 3f;
            _health = maxHealth;
        }

        public void ConfigureAsCrocodile(AreaBounds area, float stun = 2.5f)
        {
            netBehavior = CreatureNetBehavior.StunOnNet;
            wanderArea = area;
            stunSeconds = stun;
            maxHealth = 8f;
            meleeHitDamage = 1f;
            _health = maxHealth;
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }

        internal static void SnapBodyToGround(Transform t, float hover)
        {
            if (t == null) return;
            var origin = t.position + Vector3.up * 20f;
            var hits = Physics.RaycastAll(origin, Vector3.down, 52f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            var bestY = float.NegativeInfinity;
            for (var i = 0; i < hits.Length; i++)
            {
                var hit = hits[i];
                if (hit.collider == null) continue;
                if (hit.collider.transform == t || hit.collider.transform.IsChildOf(t))
                    continue;
                if (hit.point.y > bestY)
                    bestY = hit.point.y;
            }

            if (bestY > -1000f)
                t.position = new Vector3(t.position.x, bestY + hover, t.position.z);
        }

        private void SnapBodyToGround()
        {
            var hover = netBehavior == CreatureNetBehavior.StunOnNet ? 0.38f : 0.28f;
            SnapBodyToGround(transform, hover);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (patrolWaypoints == null) return;
            Gizmos.color = new Color(0.4f, 0.9f, 1f, 0.9f);
            Transform prev = null;
            foreach (var wp in patrolWaypoints)
            {
                if (wp == null) continue;
                Gizmos.DrawSphere(wp.position, 0.15f);
                if (prev != null)
                    Gizmos.DrawLine(prev.position, wp.position);
                prev = wp;
            }
        }
#endif
    }
}
