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

        [Header("Chase")]
        [SerializeField] private float stopChaseDistance = 0.9f;

        private CreatureSense _sense;
        private CreatureMode _mode = CreatureMode.Passive;
        private int _patrolIndex;
        private float _waitUntil;
        private Vector3 _wanderTarget;
        private float _nextWanderPick;
        private float _stunUntil;
        private bool _caught;
        private Vector3 _moveDir = Vector3.forward;

        public CreatureMode Mode => _mode;
        public bool IsActive => _mode == CreatureMode.Active;
        public bool IsStunned => Time.time < _stunUntil;
        public bool IsCaught => _caught;
        public bool IsNetHittable => !_caught && !IsStunned;

        private void Awake()
        {
            _sense = GetComponent<CreatureSense>();
            _sense.EnsurePlayer();
            if (wanderArea != null)
                _wanderTarget = wanderArea.RandomPointInside();
            else
                _wanderTarget = transform.position;

            if (patrolWaypoints != null && patrolWaypoints.Length > 0 && patrolWaypoints[0] != null)
                _patrolIndex = 0;
        }

        private void Update()
        {
            if (_caught) return;

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

        public NetHitResult OnNetHit(NetHitInfo info)
        {
            if (_caught || IsStunned)
                return NetHitResult.Ignored;

            if (netBehavior == CreatureNetBehavior.CatchOnNet)
            {
                Catch();
                return NetHitResult.Caught;
            }

            _stunUntil = Time.time + Mathf.Max(0.2f, stunSeconds);
            _mode = CreatureMode.Active; // Still wants the player after stun.
            return NetHitResult.Stunned;
        }

        public void Catch()
        {
            if (_caught) return;
            _caught = true;
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
        }

        public void ConfigureAsCrocodile(AreaBounds area, float stun = 2.5f)
        {
            netBehavior = CreatureNetBehavior.StunOnNet;
            wanderArea = area;
            stunSeconds = stun;
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
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
