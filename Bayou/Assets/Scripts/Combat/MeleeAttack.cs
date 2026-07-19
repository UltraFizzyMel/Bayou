using UnityEngine;

namespace Bayou.Combat
{
    /// <summary>
    /// Arc melee swing shared by player fishing-rod and hostile NPCs.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MeleeAttack : MonoBehaviour
    {
        [Header("Strike")]
        [SerializeField] private float damage = 12f;
        [SerializeField] private float range = 1.8f;
        [SerializeField] private float arcDegrees = 110f;
        [SerializeField] private float cooldown = 0.55f;
        [SerializeField] private float windupSeconds = 0.12f;
        [SerializeField] private float activeSeconds = 0.14f;
        [SerializeField] private Transform attackOrigin;
        [SerializeField] private LayerMask hitMask = ~0;
        [SerializeField] private CombatTeam attackerTeam = CombatTeam.Player;
        [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Collide;

        [Header("Feel")]
        [SerializeField] private bool faceWishDirection = true;
        [SerializeField] private float lungeDistance;

        public bool IsAttacking { get; private set; }
        public float CooldownRemaining => Mathf.Max(0f, _nextReadyTime - Time.time);

        private float _nextReadyTime;
        private float _windupEnd;
        private float _activeEnd;
        private bool _didHitPass;
        private readonly Collider[] _hits = new Collider[24];

        public void SetAttackerTeam(CombatTeam team) => attackerTeam = team;

        public bool TryBeginAttack(Vector3? faceDirection = null)
        {
            if (!isActiveAndEnabled || IsAttacking) return false;
            if (Time.time < _nextReadyTime) return false;

            if (faceWishDirection && faceDirection.HasValue)
            {
                var dir = faceDirection.Value;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.001f)
                    transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
            }

            IsAttacking = true;
            _didHitPass = false;
            _windupEnd = Time.time + Mathf.Max(0f, windupSeconds);
            _activeEnd = _windupEnd + Mathf.Max(0.02f, activeSeconds);
            _nextReadyTime = Time.time + Mathf.Max(0.05f, cooldown);

            if (lungeDistance > 0f)
            {
                var rb = GetComponent<Rigidbody>();
                if (rb != null && !rb.isKinematic)
                {
                    var fwd = transform.forward;
                    fwd.y = 0f;
                    if (fwd.sqrMagnitude > 0.001f)
                        rb.AddForce(fwd.normalized * lungeDistance, ForceMode.VelocityChange);
                }
            }

            return true;
        }

        private void Update()
        {
            if (!IsAttacking) return;

            if (!_didHitPass && Time.time >= _windupEnd)
            {
                _didHitPass = true;
                DealHits();
            }

            if (Time.time >= _activeEnd)
                IsAttacking = false;
        }

        private void DealHits()
        {
            var origin = attackOrigin != null ? attackOrigin.position : transform.position + Vector3.up * 0.9f;
            var forward = transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
            forward.Normalize();

            var count = Physics.OverlapSphereNonAlloc(origin, range, _hits, hitMask, triggerInteraction);
            var halfArc = Mathf.Clamp(arcDegrees, 1f, 360f) * 0.5f;

            for (var i = 0; i < count; i++)
            {
                var col = _hits[i];
                if (col == null) continue;
                if (col.transform == transform || col.transform.IsChildOf(transform))
                    continue;

                var health = col.GetComponentInParent<Health>();
                if (health == null || health.IsDead) continue;
                if (!health.CanBeDamagedBy(attackerTeam)) continue;

                var to = col.bounds.center - origin;
                to.y = 0f;
                if (to.sqrMagnitude < 0.0001f)
                {
                    health.ApplyDamage(damage, gameObject, attackerTeam);
                    continue;
                }

                if (Vector3.Angle(forward, to) <= halfArc)
                    health.ApplyDamage(damage, gameObject, attackerTeam);
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            var origin = attackOrigin != null ? attackOrigin.position : transform.position + Vector3.up * 0.9f;
            Gizmos.color = new Color(1f, 0.35f, 0.15f, 0.35f);
            Gizmos.DrawWireSphere(origin, range);
        }
#endif
    }
}
