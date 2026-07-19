using Bayou.Combat;
using Bayou.Player;
using UnityEngine;

namespace Bayou.Enemy
{
    public enum HostileNpcState
    {
        Patrol,
        Chase,
        Attack,
        Return,
        Dead
    }

    /// <summary>
    /// Patrol → detect → chase → attack. Uses <see cref="EnemyMotor"/> (no NavMesh required).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyMotor))]
    [RequireComponent(typeof(Health))]
    [RequireComponent(typeof(MeleeAttack))]
    public sealed class HostileNpcAI : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private Transform player;
        [SerializeField] private string playerTag = "Player";

        [Header("Ranges")]
        [SerializeField] private float detectRadius = 8f;
        [SerializeField] private float loseRadius = 14f;
        [SerializeField] private float attackRange = 1.7f;
        [SerializeField] private bool requireLineOfSight = true;
        [SerializeField] private LayerMask losBlockers = ~0;

        [Header("Patrol")]
        [SerializeField] private Transform[] patrolPoints;
        [SerializeField] private float patrolWaitSeconds = 1.2f;
        [SerializeField] private float patrolPointReachDistance = 0.6f;
        [SerializeField] private float wanderRadius = 4f;

        [Header("Speeds")]
        [SerializeField] private float patrolSpeed = 2.2f;
        [SerializeField] private float chaseSpeed = 4.2f;

        public HostileNpcState State { get; private set; } = HostileNpcState.Patrol;

        private EnemyMotor _motor;
        private Health _health;
        private MeleeAttack _melee;
        private Vector3 _spawnPosition;
        private int _patrolIndex;
        private float _waitUntil;
        private Vector3 _wanderTarget;
        private bool _hasWanderTarget;
        private bool _engaged;

        private void Awake()
        {
            _motor = GetComponent<EnemyMotor>();
            _health = GetComponent<Health>();
            _melee = GetComponent<MeleeAttack>();
            _spawnPosition = transform.position;

            _health.SetTeam(CombatTeam.Enemy);
            _melee.SetAttackerTeam(CombatTeam.Enemy);

            if (player == null)
            {
                var p = GameObject.FindGameObjectWithTag(playerTag);
                if (p != null) player = p.transform;
                else
                {
                    var motor = FindFirstObjectByType<BayouCharacterMotor>();
                    if (motor != null) player = motor.transform;
                }
            }
        }

        private void OnEnable()
        {
            _health.Died += OnDied;
        }

        private void OnDisable()
        {
            _health.Died -= OnDied;
            SetEngaged(false);
        }

        private void OnDied()
        {
            State = HostileNpcState.Dead;
            SetEngaged(false);
            _motor.Stop();
            enabled = false;
        }

        private void Update()
        {
            if (State == HostileNpcState.Dead || _health.IsDead) return;

            if (_melee.IsAttacking)
            {
                _motor.Stop();
                return;
            }

            switch (State)
            {
                case HostileNpcState.Patrol:
                    TickPatrol();
                    if (CanDetectPlayer())
                        EnterChase();
                    break;

                case HostileNpcState.Chase:
                    TickChase();
                    break;

                case HostileNpcState.Attack:
                    TickAttack();
                    break;

                case HostileNpcState.Return:
                    TickReturn();
                    break;
            }
        }

        private void EnterChase()
        {
            State = HostileNpcState.Chase;
            _motor.MaxSpeed = chaseSpeed;
            SetEngaged(true);
        }

        private void EnterPatrol()
        {
            State = HostileNpcState.Patrol;
            _motor.MaxSpeed = patrolSpeed;
            SetEngaged(false);
            _waitUntil = 0f;
        }

        private void EnterReturn()
        {
            State = HostileNpcState.Return;
            _motor.MaxSpeed = patrolSpeed;
            SetEngaged(false);
        }

        private void TickPatrol()
        {
            _motor.MaxSpeed = patrolSpeed;

            if (Time.time < _waitUntil)
            {
                _motor.Stop();
                return;
            }

            if (patrolPoints != null && patrolPoints.Length > 0)
            {
                var point = patrolPoints[_patrolIndex];
                if (point == null)
                {
                    _patrolIndex = (_patrolIndex + 1) % patrolPoints.Length;
                    return;
                }

                if (MoveToward(point.position, patrolPointReachDistance))
                {
                    _patrolIndex = (_patrolIndex + 1) % patrolPoints.Length;
                    _waitUntil = Time.time + patrolWaitSeconds;
                    _motor.Stop();
                }
            }
            else
            {
                if (!_hasWanderTarget || FlatDistance(transform.position, _wanderTarget) < patrolPointReachDistance)
                {
                    var offset = Random.insideUnitSphere * wanderRadius;
                    offset.y = 0f;
                    _wanderTarget = _spawnPosition + offset;
                    _hasWanderTarget = true;
                    _waitUntil = Time.time + patrolWaitSeconds * Random.Range(0.5f, 1f);
                    _motor.Stop();
                    return;
                }

                MoveToward(_wanderTarget, patrolPointReachDistance);
            }
        }

        private void TickChase()
        {
            if (player == null || !IsPlayerInRadius(loseRadius))
            {
                EnterReturn();
                return;
            }

            var dist = FlatDistance(transform.position, player.position);
            if (dist <= attackRange)
            {
                State = HostileNpcState.Attack;
                _motor.Stop();
                return;
            }

            _motor.MaxSpeed = chaseSpeed;
            MoveToward(player.position, 0.05f);
        }

        private void TickAttack()
        {
            if (player == null || !IsPlayerInRadius(loseRadius))
            {
                EnterReturn();
                return;
            }

            var toPlayer = Flat(player.position - transform.position);
            var dist = toPlayer.magnitude;

            if (dist > attackRange * 1.15f)
            {
                EnterChase();
                return;
            }

            _motor.Stop();
            if (toPlayer.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(toPlayer.normalized, Vector3.up);

            _melee.TryBeginAttack(toPlayer);
        }

        private void TickReturn()
        {
            var home = GetHomePosition();
            if (MoveToward(home, patrolPointReachDistance))
                EnterPatrol();

            if (CanDetectPlayer())
                EnterChase();
        }

        private Vector3 GetHomePosition()
        {
            if (patrolPoints != null && patrolPoints.Length > 0 && patrolPoints[0] != null)
                return patrolPoints[0].position;
            return _spawnPosition;
        }

        private static readonly RaycastHit[] LosHits = new RaycastHit[8];

        private bool CanDetectPlayer()
        {
            if (player == null) return false;
            if (!IsPlayerInRadius(detectRadius)) return false;
            if (!requireLineOfSight) return true;

            var origin = transform.position + Vector3.up * 1.2f;
            var target = player.position + Vector3.up * 1.0f;
            var dir = target - origin;
            var dist = dir.magnitude;
            if (dist < 0.01f) return true;

            var count = Physics.RaycastNonAlloc(
                origin,
                dir.normalized,
                LosHits,
                dist,
                losBlockers,
                QueryTriggerInteraction.Ignore);

            var nearest = float.MaxValue;
            Transform nearestTf = null;
            for (var i = 0; i < count; i++)
            {
                var hit = LosHits[i];
                if (hit.collider == null) continue;
                if (hit.transform == transform || hit.transform.IsChildOf(transform))
                    continue;
                if (hit.distance < nearest)
                {
                    nearest = hit.distance;
                    nearestTf = hit.transform;
                }
            }

            if (nearestTf == null) return true;
            return nearestTf == player || nearestTf.IsChildOf(player);
        }

        private bool IsPlayerInRadius(float radius)
        {
            if (player == null) return false;
            return FlatDistance(transform.position, player.position) <= radius;
        }

        private bool MoveToward(Vector3 worldTarget, float arriveDistance)
        {
            var to = Flat(worldTarget - transform.position);
            if (to.magnitude <= arriveDistance)
            {
                _motor.Stop();
                return true;
            }

            _motor.SetWishDirection(to);
            return false;
        }

        private void SetEngaged(bool engaged)
        {
            if (_engaged == engaged) return;
            _engaged = engaged;
            CombatPresence.SetEngaged(this, engaged);
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }

        private static float FlatDistance(Vector3 a, Vector3 b) => Flat(a - b).magnitude;

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, detectRadius);
            Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.25f);
            Gizmos.DrawWireSphere(transform.position, loseRadius);
            Gizmos.color = new Color(1f, 0.1f, 0.1f, 0.45f);
            Gizmos.DrawWireSphere(transform.position, attackRange);
        }
#endif
    }
}
