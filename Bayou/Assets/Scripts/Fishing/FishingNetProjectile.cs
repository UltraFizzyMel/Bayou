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

    /// <summary>
    /// Thrown net: flies, then plants statically in water. Fish swim toward it during attract.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class FishingNetProjectile : MonoBehaviour
    {
        public static FishingNetProjectile ActiveInWater { get; private set; }

        [Header("Physics")]
        [Tooltip("Auto-destroy only if the net never lands in water. 0 = never.")]
        [SerializeField] private float missLifetimeSeconds = 8f;
        [SerializeField] private bool stickOnDryLand = true;

        [Header("Water")]
        [SerializeField] private LayerMask waterLayers;
        [SerializeField] private bool acceptWaterTagFallback = true;
        [SerializeField] private float waterSnapOffset = 0.06f;
        [SerializeField] private float plantDepth = 0.15f;

        private Rigidbody _rb;
        private bool _hasLanded;
        private FishingNetVisual _visual;

        public FishingNetPhase Phase { get; private set; } = FishingNetPhase.Flying;
        public Vector3 PlantPosition => transform.position;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            EnsurePhases();
            EnsureVisual();

            var attract = GetComponent<FishingAttractPhase>();
            if (attract != null)
                attract.enabled = false;

            var reel = GetComponent<FishingReelPhase>();
            if (reel != null)
                reel.enabled = false;

            if (missLifetimeSeconds > 0f)
                Invoke(nameof(DestroyAfterMissLifetime), missLifetimeSeconds);
        }

        private void OnDestroy()
        {
            if (ActiveInWater == this)
                ActiveInWater = null;

            var attract = GetComponent<FishingAttractPhase>();
            if (attract != null)
                attract.AttractComplete -= OnAttractCompleteFromPhase;
        }

        public void Launch(Vector3 initialVelocity)
        {
            Phase = FishingNetPhase.Flying;
            _hasLanded = false;
            if (ActiveInWater == this)
                ActiveInWater = null;

            _rb.isKinematic = false;
            _rb.useGravity = true;
            _rb.linearVelocity = initialVelocity;

            EnsurePhases();
            EnsureVisual();
            _visual?.ShowInFlight();

            var attract = GetComponent<FishingAttractPhase>();
            if (attract != null)
            {
                attract.enabled = false;
                attract.AttractComplete -= OnAttractCompleteFromPhase;
                attract.AttractComplete += OnAttractCompleteFromPhase;
            }
        }

        private void EnsureVisual()
        {
            _visual = GetComponent<FishingNetVisual>();
            if (_visual == null)
                _visual = gameObject.AddComponent<FishingNetVisual>();
        }

        public void CancelAndDestroy()
        {
            CancelMissLifetime();

            var reel = GetComponent<FishingReelPhase>();
            if (reel != null && reel.IsActive)
            {
                reel.CancelReel();
                return;
            }

            var attract = GetComponent<FishingAttractPhase>();
            if (attract != null)
                attract.CancelAttract();

            if (ActiveInWater == this)
                ActiveInWater = null;

            if (this != null && gameObject != null)
                Destroy(gameObject);
        }

        private void OnAttractCompleteFromPhase()
        {
            Phase = FishingNetPhase.AttractComplete;
            CancelMissLifetime();

            var reel = GetComponent<FishingReelPhase>();
            if (reel == null)
                reel = gameObject.AddComponent<FishingReelPhase>();
            reel.BeginReel();
        }

        private void EnsurePhases()
        {
            if (GetComponent<FishingAttractPhase>() == null)
                gameObject.AddComponent<FishingAttractPhase>();
            if (GetComponent<FishingReelPhase>() == null)
                gameObject.AddComponent<FishingReelPhase>();
        }

        private void DestroyAfterMissLifetime()
        {
            // Only despawn misses — planted nets stay until cancel / catch / reel end.
            if (Phase == FishingNetPhase.LandedInWater || Phase == FishingNetPhase.AttractComplete)
                return;
            Destroy(gameObject);
        }

        private void CancelMissLifetime()
        {
            CancelInvoke(nameof(DestroyAfterMissLifetime));
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
            ActiveInWater = this;

            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _rb.isKinematic = true;
            _rb.useGravity = false;

            if (collision != null && collision.contactCount > 0)
            {
                var c = collision.GetContact(0);
                transform.position = c.point + c.normal * waterSnapOffset - Vector3.up * plantDepth;
            }
            else
            {
                var p = transform.position;
                p.y -= plantDepth;
                transform.position = p;
            }

            CancelMissLifetime();
            EnsureVisual();
            _visual?.ShowPlanted();

            Bayou.Audio.FishingAudio.Resolve()?.PlayLanding();

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

            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _rb.isKinematic = true;
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
    }
}
