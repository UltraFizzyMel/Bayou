using UnityEngine;

namespace Bayou.Creatures
{
    /// <summary>
    /// Vision cone + always-on minimum sense. Tall grass and solid occluders break vision
    /// (not the min-sense bubble).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CreatureSense : MonoBehaviour
    {
        [Header("Ranges")]
        [Tooltip("Always detect the player inside this radius (ignores LoS / tall grass).")]
        [SerializeField] private float minSenseRange = 2.2f;

        [Tooltip("Max distance for the vision cone.")]
        [SerializeField] private float visionRange = 10f;

        [Tooltip("Full cone angle in degrees (facing forward).")]
        [SerializeField] [Range(10f, 180f)] private float visionAngleDegrees = 70f;

        [Header("Line of sight")]
        [SerializeField] private LayerMask occlusionMask = ~0;
        [SerializeField] private float eyeHeight = 0.6f;
        [SerializeField] private float playerChestHeight = 1.1f;

        [Header("Memory")]
        [Tooltip("Stay Active this long after last sensing the player.")]
        [SerializeField] private float loseSightGraceSeconds = 1.25f;

        public float MinSenseRange => minSenseRange;
        public float VisionRange => visionRange;
        public float VisionAngleDegrees => visionAngleDegrees;

        private Transform _player;
        private float _lastSensedTime = -999f;

        public Transform Player => _player;
        public bool HasRecentSense => Time.time - _lastSensedTime <= loseSightGraceSeconds;

        public void SetPlayer(Transform player) => _player = player;

        public void EnsurePlayer()
        {
            if (_player != null) return;
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null)
                _player = p.transform;
            else
            {
                var motor = FindFirstObjectByType<Bayou.Player.BayouCharacterMotor>();
                if (motor != null)
                    _player = motor.transform;
            }
        }

        /// <summary>True if the player is currently sensed (updates memory).</summary>
        public bool TrySensePlayer(out Transform player)
        {
            player = null;
            EnsurePlayer();
            if (_player == null) return false;

            if (EvaluateSense(_player))
            {
                _lastSensedTime = Time.time;
                player = _player;
                return true;
            }

            return false;
        }

        public bool EvaluateSense(Transform player)
        {
            if (player == null) return false;

            var eye = transform.position + Vector3.up * eyeHeight;
            var target = player.position + Vector3.up * playerChestHeight;
            var to = target - eye;
            to.y = 0f;
            var dist = to.magnitude;
            if (dist < 0.01f)
                return true;

            // Minimum bubble: always sense.
            if (dist <= minSenseRange)
                return true;

            if (dist > visionRange)
                return false;

            // Tall grass hides from vision (not from min sense). A lit lantern gives you away.
            if (TallGrassVolume.IsPlayerHidden(player) && !Bayou.Fishing.HeldLantern.IsAnyLit)
                return false;

            var forward = transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-6f)
                forward = Vector3.forward;
            forward.Normalize();

            var dir = to / dist;
            var half = visionAngleDegrees * 0.5f;
            if (Vector3.Angle(forward, dir) > half)
                return false;

            // Solid occlusion blocks vision (ignore triggers + this creature's own colliders).
            var castDir = target - eye;
            var castDist = castDir.magnitude;
            if (castDist > 0.05f)
            {
                var hits = Physics.RaycastAll(eye, castDir.normalized, castDist, occlusionMask,
                    QueryTriggerInteraction.Ignore);
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                foreach (var hit in hits)
                {
                    if (hit.collider == null) continue;
                    if (hit.collider.transform == transform || hit.collider.transform.IsChildOf(transform))
                        continue;
                    if (IsPlayerHit(hit.collider, player))
                        break;
                    return false;
                }
            }

            return true;
        }

        private static bool IsPlayerHit(Collider col, Transform player)
        {
            if (col == null || player == null) return false;
            return col.transform == player ||
                   col.transform.IsChildOf(player) ||
                   col.transform.root == player.root;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            var origin = transform.position + Vector3.up * 0.05f;
            Gizmos.color = new Color(1f, 0.35f, 0.2f, 0.55f);
            Gizmos.DrawWireSphere(origin, minSenseRange);

            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.35f);
            var half = visionAngleDegrees * 0.5f;
            var fwd = transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-6f) fwd = Vector3.forward;
            fwd.Normalize();
            var left = Quaternion.Euler(0f, -half, 0f) * fwd;
            var right = Quaternion.Euler(0f, half, 0f) * fwd;
            Gizmos.DrawLine(origin, origin + left * visionRange);
            Gizmos.DrawLine(origin, origin + right * visionRange);
            const int arcs = 16;
            var prev = origin + left * visionRange;
            for (var i = 1; i <= arcs; i++)
            {
                var t = Mathf.Lerp(-half, half, i / (float)arcs);
                var d = Quaternion.Euler(0f, t, 0f) * fwd;
                var next = origin + d * visionRange;
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }
#endif
    }
}
