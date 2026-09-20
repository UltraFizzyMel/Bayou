using Bayou.Environment;
using UnityEngine;

namespace Bayou.Player
{
    /// <summary>
    /// Detects water via trigger overlap. Reports two depth levels: wade and swim.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BayouWaterSensor : MonoBehaviour
    {
        [Header("Water layer")]
        [Tooltip("Assign your Water layer mask here.")]
        [SerializeField] private LayerMask waterLayers;

        [Header("Overlap (best for trigger volumes & shallow water)")]
        [SerializeField] private float footOverlapYOffset = 0.08f;
        [SerializeField] private float footOverlapRadius = 0.38f;

        [Header("Raycast (extra — hits surface under feet)")]
        [SerializeField] private Vector3 rayOriginOffset = new(0f, 0.35f, 0f);
        [SerializeField] private float rayLength = 3f;

        [Header("Trigger messages")]
        [SerializeField] private bool useTriggerMessages = true;

        [Tooltip("If true, tagged Water counts without WaterVolume on that collider.")]
        [SerializeField] private bool acceptWaterTagWithoutComponent = true;

        [SerializeField] private float chestHeight = 1.05f;

        private readonly System.Collections.Generic.HashSet<Collider> _activeWaterTriggers = new();
        private static readonly Collider[] OverlapHits = new Collider[16];

        public bool InWater => Depth != WaterDepthLevel.None;
        public bool IsWading => Depth == WaterDepthLevel.Wade;
        public bool IsSwimming => Depth == WaterDepthLevel.Swim;
        public WaterDepthLevel Depth { get; private set; }
        public float WaterSurfaceY { get; private set; }
        public float Submersion { get; private set; }

        /// <summary>World Y the motor should hold while swimming (chest near the surface).</summary>
        public float SwimHoldY => WaterSurfaceY - Mathf.Max(0.72f, chestHeight * 0.65f);

        private WaterDepthLevel _latchedDepth;
        private float _swimLatchUntil;

        private Vector3 FeetPosition => transform.position + new Vector3(0f, footOverlapYOffset, 0f);

        private void OnTriggerEnter(Collider other)
        {
            if (!useTriggerMessages) return;
            if (IsWaterCollider(other))
                _activeWaterTriggers.Add(other);
            Refresh();
        }

        private void OnTriggerStay(Collider other)
        {
            if (!useTriggerMessages) return;
            if (IsWaterCollider(other))
                _activeWaterTriggers.Add(other);
        }

        private void OnTriggerExit(Collider other)
        {
            if (!useTriggerMessages) return;
            _activeWaterTriggers.Remove(other);
            Refresh();
        }

        private void FixedUpdate()
        {
            Refresh();
        }

        private void Refresh()
        {
            PruneTriggers();

            var feet = FeetPosition;
            var depth = WaterDepthLevel.None;
            var surface = feet.y;
            var foundSurface = false;

            void Consider(Collider col)
            {
                if (col == null || !col.enabled) return;
                var vol = col.GetComponent<WaterVolume>() ?? col.GetComponentInParent<WaterVolume>();
                var top = vol != null ? vol.SurfaceY : col.bounds.max.y;
                var sample = vol != null
                    ? vol.Evaluate(feet)
                    : (top - feet.y >= 0.12f ? WaterDepthLevel.Wade : WaterDepthLevel.None);
                if (sample > depth)
                    depth = sample;

                if (sample != WaterDepthLevel.None && (!foundSurface || top > surface))
                {
                    surface = top;
                    foundSurface = true;
                }
            }

            if (useTriggerMessages)
            {
                foreach (var col in _activeWaterTriggers)
                    Consider(col);
            }

            if (waterLayers.value != 0)
            {
                var count = Physics.OverlapSphereNonAlloc(
                    feet, footOverlapRadius, OverlapHits, waterLayers, QueryTriggerInteraction.Collide);
                for (var i = 0; i < count; i++)
                    Consider(OverlapHits[i]);

                if (Physics.Raycast(transform.position + rayOriginOffset, Vector3.down, out var hit, rayLength,
                        waterLayers, QueryTriggerInteraction.Collide))
                    Consider(hit.collider);
            }

            if (depth == WaterDepthLevel.None && _activeWaterTriggers.Count > 0 &&
                foundSurface && surface - feet.y >= 0.12f && !StandingOnBank(feet, surface, true))
                depth = WaterDepthLevel.Wade;

            if (depth != WaterDepthLevel.None && StandingOnBank(feet, surface, foundSurface))
                depth = WaterDepthLevel.None;

            WaterSurfaceY = foundSurface ? surface : transform.position.y;
            Submersion = InWaterOr(depth) ? WaterSurfaceY - feet.y : 0f;

            // Deep-water swim/wade flicker snaps hold poses and jittered equipped items.
            if (depth == WaterDepthLevel.Swim)
            {
                _latchedDepth = WaterDepthLevel.Swim;
                _swimLatchUntil = Time.time + 0.4f;
            }
            else if (_latchedDepth == WaterDepthLevel.Swim &&
                     depth != WaterDepthLevel.None &&
                     Time.time < _swimLatchUntil)
            {
                depth = WaterDepthLevel.Swim;
            }
            else if (depth == WaterDepthLevel.None)
            {
                _latchedDepth = WaterDepthLevel.None;
            }

            Depth = depth;
        }

        private bool StandingOnBank(Vector3 feet, float surface, bool foundSurface)
        {
            var origin = feet + Vector3.up * 0.4f;
            if (!Physics.Raycast(origin, Vector3.down, out var hit, 1.6f, ~0, QueryTriggerInteraction.Ignore))
                return false;
            if (IsWaterCollider(hit.collider))
                return false;
            var top = foundSurface ? surface : hit.point.y;
            return hit.point.y >= top - 0.08f;
        }

        private static bool InWaterOr(WaterDepthLevel depth) => depth != WaterDepthLevel.None;

        private void PruneTriggers()
        {
            if (_activeWaterTriggers.Count == 0) return;
            _activeWaterTriggers.RemoveWhere(c => c == null || !c.enabled || !c.gameObject.activeInHierarchy);
        }

        private bool IsWaterCollider(Collider other)
        {
            if (other == null) return false;

            var vol = other.GetComponent<WaterVolume>() ?? other.GetComponentInParent<WaterVolume>();
            if (vol != null)
                return vol.Matches(other.gameObject) || vol.Matches(vol.gameObject);

            if (waterLayers.value != 0 && ((1 << other.gameObject.layer) & waterLayers.value) != 0)
                return true;

            return acceptWaterTagWithoutComponent && other.CompareTag("Water");
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Depth == WaterDepthLevel.Swim
                ? new Color(0.1f, 0.3f, 0.95f, 0.45f)
                : new Color(0.2f, 0.6f, 1f, 0.35f);
            Gizmos.DrawWireSphere(FeetPosition, footOverlapRadius);
            var o = transform.position + rayOriginOffset;
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(o, o + Vector3.down * rayLength);
        }
#endif
    }
}
