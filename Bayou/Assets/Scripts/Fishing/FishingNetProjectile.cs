using Bayou.Environment;
using UnityEngine;

namespace Bayou.Fishing
{
    public enum FishingNetPhase
    {
        Flying,
        LandedOnLand,
        LandedInWater,
        AttractComplete
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class FishingNetProjectile : MonoBehaviour
    {
        [Header("Physics")]
        [SerializeField] private float lifeSeconds = 12f;
        [SerializeField] private bool stickOnDryLand = true;

        [Header("Water (solid collider + layer — recommended)")]
        [Tooltip("Assign the same Water layer as your water plane/mesh.")]
        [SerializeField] private LayerMask waterLayers;

        [SerializeField] private bool acceptWaterTagFallback = true;

        [Tooltip("Small lift along surface normal when snapping to water hit.")]
        [SerializeField] private float waterSnapOffset = 0.06f;

        private Rigidbody rb;
        private bool _hasLanded;

        public FishingNetPhase Phase { get; private set; } = FishingNetPhase.Flying;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            var attract = GetComponent<FishingAttractPhase>();
            if (attract != null)
                attract.enabled = false;

            if (lifeSeconds > 0f)
                Invoke(nameof(DestroyAfterLifetime), lifeSeconds);
        }

        private void OnDestroy()
        {
            var attract = GetComponent<FishingAttractPhase>();
            if (attract != null)
                attract.AttractComplete -= OnAttractCompleteFromPhase;
        }

        public void Launch(Vector3 initialVelocity)
        {
            Phase = FishingNetPhase.Flying;
            _hasLanded = false;
            rb.isKinematic = false;
            rb.linearVelocity = initialVelocity;

            var attract = GetComponent<FishingAttractPhase>();
            if (attract != null)
            {
                attract.enabled = false;
                attract.AttractComplete -= OnAttractCompleteFromPhase;
                attract.AttractComplete += OnAttractCompleteFromPhase;
            }
        }

        private void OnAttractCompleteFromPhase()
        {
            Phase = FishingNetPhase.AttractComplete;
        }

        private void DestroyAfterLifetime()
        {
            Destroy(gameObject);
        }

        private void CancelLifetime()
        {
            CancelInvoke(nameof(DestroyAfterLifetime));
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (_hasLanded) return;

            if (IsWater(collision.collider))
            {
                LandInWater(collision);
                return;
            }

            LandOnDry(collision);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_hasLanded) return;
            if (!IsWater(other)) return;
            LandInWater(null);
        }

        private void LandInWater(Collision collision)
        {
            _hasLanded = true;
            Phase = FishingNetPhase.LandedInWater;

            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;

            if (collision != null && collision.contactCount > 0)
            {
                var c = collision.GetContact(0);
                transform.position = c.point + c.normal * waterSnapOffset;
            }

            CancelLifetime();

            var attract = GetComponent<FishingAttractPhase>();
            if (attract != null)
                attract.BeginAttract();
        }

        private void LandOnDry(Collision _)
        {
            _hasLanded = true;
            Phase = FishingNetPhase.LandedOnLand;

            if (!stickOnDryLand)
                return;

            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        private bool IsWater(Collider other)
        {
            if (other == null) return false;

            if (waterLayers.value != 0)
            {
                var bit = 1 << other.gameObject.layer;
                if ((waterLayers.value & bit) != 0)
                {
                    var vol = other.GetComponent<WaterVolume>();
                    if (vol != null)
                        return vol.Matches(other.gameObject);
                    return true;
                }
            }

            var w = other.GetComponent<WaterVolume>();
            if (w != null)
                return w.Matches(other.gameObject);

            return acceptWaterTagFallback && other.CompareTag("Water");
        }

        /// <summary>Optional hook for Part 3 (reeling). Call when you're ready to transition.</summary>
        public void MarkReadyForReel()
        {
            if (Phase == FishingNetPhase.LandedInWater || Phase == FishingNetPhase.AttractComplete)
                Phase = FishingNetPhase.AttractComplete;
        }
    }
}
