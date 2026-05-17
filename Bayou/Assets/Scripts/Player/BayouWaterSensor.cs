using Bayou.Environment;
using UnityEngine;

namespace Bayou.Player
{
    /// <summary>
    /// Detects "in water" using overlap + downward raycast against the Water layer.
    /// Supports trigger colliders (recommended for walk-through surface water — no vertical edges blocking entry).
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

        [Header("Trigger messages (optional backup)")]
        [SerializeField] private bool useTriggerMessages = true;

        [Tooltip("If true, tagged Water counts without WaterVolume on that collider.")]
        [SerializeField] private bool acceptWaterTagWithoutComponent = true;

        private readonly System.Collections.Generic.HashSet<Collider> _activeWaterTriggers =
            new System.Collections.Generic.HashSet<Collider>();

        public bool InWater { get; private set; }

        private void OnTriggerEnter(Collider other)
        {
            if (!useTriggerMessages) return;
            if (IsWaterCollider(other))
                _activeWaterTriggers.Add(other);
            RefreshInWater();
        }

        private void OnTriggerExit(Collider other)
        {
            if (!useTriggerMessages) return;
            _activeWaterTriggers.Remove(other);
            RefreshInWater();
        }

        private void FixedUpdate()
        {
            RefreshInWater();
        }

        private void RefreshInWater()
        {
            if (waterLayers.value == 0)
            {
                InWater = useTriggerMessages && _activeWaterTriggers.Count > 0;
                return;
            }

            InWater = OverlapFeetInWater() ||
                      RaycastHitsWater() ||
                      (useTriggerMessages && _activeWaterTriggers.Count > 0);
        }

        private bool OverlapFeetInWater()
        {
            var p = transform.position + new Vector3(0f, footOverlapYOffset, 0f);
            return Physics.CheckSphere(p, footOverlapRadius, waterLayers, QueryTriggerInteraction.Collide);
        }

        private bool RaycastHitsWater()
        {
            var origin = transform.position + rayOriginOffset;
            // Include triggers so thin water planes / trigger volumes register.
            if (!Physics.Raycast(origin, Vector3.down, out var hit, rayLength, waterLayers,
                    QueryTriggerInteraction.Collide))
                return false;

            var vol = hit.collider.GetComponent<WaterVolume>();
            if (vol != null)
                return vol.Matches(hit.collider.gameObject);

            return true;
        }

        private bool IsWaterCollider(Collider other)
        {
            if (other == null) return false;

            var vol = other.GetComponent<WaterVolume>();
            if (vol != null)
                return vol.Matches(other.gameObject);

            return acceptWaterTagWithoutComponent && other.CompareTag("Water");
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (waterLayers.value == 0) return;
            Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.35f);
            var p = transform.position + new Vector3(0f, footOverlapYOffset, 0f);
            Gizmos.DrawWireSphere(p, footOverlapRadius);
            var o = transform.position + rayOriginOffset;
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(o, o + Vector3.down * rayLength);
        }
#endif
    }
}
