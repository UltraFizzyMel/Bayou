using Bayou.Environment;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Bayou.Fishing
{
    public enum FishingNetPhase
    {
        Flying,
        LandedOnLand,
        LandedInWater,
        AttractComplete,
        Reeling,
        CatchResolved
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

        [Header("After catch / fail")]
        [SerializeField] private float destroyDelayAfterResolve = 0.65f;

        private Rigidbody rb;
        private bool _hasLanded;
        private FishingAttractPhase _attract;
        private FishingReelPhase _reel;

        public FishingNetPhase Phase { get; private set; } = FishingNetPhase.Flying;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            EnsurePhases();

            if (_attract != null)
                _attract.enabled = false;
            if (_reel != null)
                _reel.enabled = false;

            if (lifeSeconds > 0f)
                Invoke(nameof(DestroyAfterLifetime), lifeSeconds);
        }

        private void EnsurePhases()
        {
            _attract = GetComponent<FishingAttractPhase>();
            if (_attract == null)
                _attract = gameObject.AddComponent<FishingAttractPhase>();

            _reel = GetComponent<FishingReelPhase>();
            if (_reel == null)
                _reel = gameObject.AddComponent<FishingReelPhase>();
        }

        private void OnDestroy()
        {
            UnbindPhaseEvents();
            FishingActivity.SetBusy(false);
        }

#if ENABLE_INPUT_SYSTEM
        /// <summary>Called by <see cref="FishingNetCaster"/> so wiggle/reel share the player's Move action.</summary>
        public void BindPlayerInput(InputActionReference moveAction)
        {
            EnsurePhases();
            _attract?.SetMoveAction(moveAction);
            _reel?.SetMoveAction(moveAction);
        }
#endif

        public void Launch(Vector3 initialVelocity)
        {
            EnsurePhases();
            Phase = FishingNetPhase.Flying;
            _hasLanded = false;
            rb.isKinematic = false;
            rb.linearVelocity = initialVelocity;

            if (_attract != null)
            {
                _attract.enabled = false;
                _attract.AttractComplete -= OnAttractCompleteFromPhase;
                _attract.AttractComplete += OnAttractCompleteFromPhase;
            }

            if (_reel != null)
            {
                _reel.enabled = false;
                _reel.ReelSuccess -= OnReelSuccess;
                _reel.ReelFail -= OnReelFail;
                _reel.ReelSuccess += OnReelSuccess;
                _reel.ReelFail += OnReelFail;
            }
        }

        private void UnbindPhaseEvents()
        {
            if (_attract != null)
                _attract.AttractComplete -= OnAttractCompleteFromPhase;
            if (_reel != null)
            {
                _reel.ReelSuccess -= OnReelSuccess;
                _reel.ReelFail -= OnReelFail;
            }
        }

        private void OnAttractCompleteFromPhase()
        {
            Phase = FishingNetPhase.AttractComplete;
            MarkReadyForReel();

            if (_reel != null)
            {
                Phase = FishingNetPhase.Reeling;
                _reel.BeginReel();
            }
            else
            {
                // No reel component — resolve immediately with a catch scoop at the net.
                ScoopAndResolve();
            }
        }

        private void OnReelSuccess()
        {
            Phase = FishingNetPhase.CatchResolved;
            ScheduleDestroy();
        }

        private void OnReelFail()
        {
            Phase = FishingNetPhase.CatchResolved;
            ScheduleDestroy();
        }

        private void ScoopAndResolve()
        {
            var count = Physics.OverlapSphereNonAlloc(
                transform.position,
                2.2f,
                BayouFishNetOverlapBuffer.Colliders,
                ~0,
                QueryTriggerInteraction.Collide);

            for (var i = 0; i < count; i++)
            {
                var c = BayouFishNetOverlapBuffer.Colliders[i];
                if (c == null) continue;
                var fish = c.GetComponentInParent<Bayou.Fish.BayouFish>();
                if (fish != null)
                    fish.TryCatchFromNet(transform.position, 2.2f);
            }

            Phase = FishingNetPhase.CatchResolved;
            FishingActivity.SetBusy(false);
            ScheduleDestroy();
        }

        private void DestroyAfterLifetime()
        {
            Destroy(gameObject);
        }

        private void ScheduleDestroy()
        {
            CancelLifetime();
            Destroy(gameObject, Mathf.Max(0.05f, destroyDelayAfterResolve));
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

            if (_attract != null)
                _attract.BeginAttract();
            else
                ScoopAndResolve();
        }

        private void LandOnDry(Collision _)
        {
            _hasLanded = true;
            Phase = FishingNetPhase.LandedOnLand;
            FishingActivity.SetBusy(false);

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

        /// <summary>Marks the net ready for Part 3 reeling.</summary>
        public void MarkReadyForReel()
        {
            if (Phase == FishingNetPhase.LandedInWater || Phase == FishingNetPhase.AttractComplete)
                Phase = FishingNetPhase.AttractComplete;
        }
    }
}
