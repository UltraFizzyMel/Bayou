using Bayou.Environment;
using Bayou.Fish;
using Bayou.Quests;
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
    /// Can also scoop a <see cref="PondShinyCollectible"/> when planted near one.
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
        [Tooltip("Ignore non-water collisions for this long after launch (avoids sticking at the player's feet).")]
        [SerializeField] private float launchGraceSeconds = 0.35f;
        [Tooltip("Must travel at least this far before dry-land stick is allowed.")]
        [SerializeField] private float minFlightDistance = 2f;
        [SerializeField] private float shinyScoopRadius = 2.4f;

        [Header("Water")]
        [SerializeField] private LayerMask waterLayers;
        [SerializeField] private bool acceptWaterTagFallback = true;
        [SerializeField] private float waterSnapOffset = 0.06f;
        [SerializeField] private float plantDepth = 0.15f;

        private Rigidbody _rb;
        private Collider _col;
        private bool _hasLanded;
        private FishingNetVisual _visual;
        private Vector3 _launchPos;
        private float _launchTime;

        public FishingNetPhase Phase { get; private set; } = FishingNetPhase.Flying;
        public Vector3 PlantPosition => transform.position;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _col = GetComponent<Collider>();
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

        public void Launch(Vector3 initialVelocity) => Launch(initialVelocity, null);

        public void Launch(Vector3 initialVelocity, GameObject casterRoot)
        {
            Phase = FishingNetPhase.Flying;
            _hasLanded = false;
            _launchPos = transform.position;
            _launchTime = Time.time;

            if (ActiveInWater == this)
                ActiveInWater = null;

            IgnoreCollisionsWith(casterRoot, true);

            _rb.isKinematic = false;
            _rb.useGravity = true;
            _rb.linearVelocity = initialVelocity;
            _rb.angularVelocity = Vector3.zero;

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

        private void IgnoreCollisionsWith(GameObject root, bool ignore)
        {
            if (root == null || _col == null) return;

            foreach (var other in root.GetComponentsInChildren<Collider>(true))
            {
                if (other != null && other != _col)
                    Physics.IgnoreCollision(_col, other, ignore);
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
            if (Phase == FishingNetPhase.LandedInWater || Phase == FishingNetPhase.AttractComplete)
                return;
            Destroy(gameObject);
        }

        private void CancelMissLifetime()
        {
            CancelInvoke(nameof(DestroyAfterMissLifetime));
        }

        private bool StillInLaunchGrace()
        {
            if (Time.time - _launchTime < launchGraceSeconds)
                return true;

            var traveled = Vector3.Distance(transform.position, _launchPos);
            return traveled < minFlightDistance;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (_hasLanded) return;

            if (IsWater(collision.collider))
            {
                LandInWater(collision);
                return;
            }

            // Don't stick to the player / ground right at the cast origin.
            if (StillInLaunchGrace())
                return;

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

            // Scoop one-time loot (shiny / rosary) if the net lands on it.
            if (PondShinyCollectible.TryScoopNear(transform.position, shinyScoopRadius) ||
                NetScoopLoot.TryScoopNear(transform.position, shinyScoopRadius))
            {
                Destroy(gameObject);
                return;
            }

            // Rod attract only makes sense if a rod-fish is nearby.
            if (!HasRodFishNearby(transform.position, 22f))
            {
                Debug.Log("[Fishing] Net planted — no rod fish here (use hand net for NET spots).");
                return;
            }

            var attract = GetComponent<FishingAttractPhase>();
            if (attract != null)
                attract.BeginAttract();
        }

        private static bool HasRodFishNearby(Vector3 pos, float radius)
        {
            var rSq = radius * radius;
            foreach (var fish in Object.FindObjectsByType<BayouFish>(FindObjectsSortMode.None))
            {
                if (fish == null || fish.IsCaught || !fish.CanCatchWith(FishCatchTool.Rod)) continue;
                var d = fish.transform.position - pos;
                d.y = 0f;
                if (d.sqrMagnitude <= rSq) return true;
            }

            return false;
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
            EnsureVisual();
            _visual?.ShowPlanted();
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
