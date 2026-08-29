using Bayou.Creatures;
using Bayou.Environment;
using Bayou.Fish;
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
        public static FishingNetProjectile Current { get; private set; }

        [Header("Physics")]
        [Tooltip("Auto-destroy only if the net never lands in water. 0 = never.")]
        [SerializeField] private float missLifetimeSeconds = 8f;
        [SerializeField] private bool stickOnDryLand = true;
        [Tooltip("Ignore non-water collisions for this long after launch (avoids sticking at the player's feet).")]
        [SerializeField] private float launchGraceSeconds = 0.35f;
        [Tooltip("Must travel at least this far before dry-land stick is allowed.")]
        [SerializeField] private float minFlightDistance = 2f;
        [SerializeField] private float shinyScoopRadius = 1.15f;
        [Tooltip("Radius used to stun/catch creatures when the net plants or strikes them in flight.")]
        [SerializeField] private float creatureHitRadius = 2.2f;

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
        /// <summary>Short hint for HUD / prompts after the bobber lands.</summary>
        public string StatusHint { get; private set; } = "";

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

        private void FixedUpdate()
        {
            if (_hasLanded || Phase != FishingNetPhase.Flying) return;
            if (StillInLaunchGrace()) return;
            TryLandInNearbyWater();
        }

        private void OnDestroy()
        {
            if (ActiveInWater == this)
                ActiveInWater = null;
            if (Current == this)
                Current = null;

            var attract = GetComponent<FishingAttractPhase>();
            if (attract != null)
                attract.AttractComplete -= OnAttractCompleteFromPhase;
        }

        public void Launch(Vector3 initialVelocity) => Launch(initialVelocity, null, null);

        public void Launch(Vector3 initialVelocity, GameObject casterRoot) =>
            Launch(initialVelocity, casterRoot, casterRoot != null ? casterRoot.transform : null);

        public void Launch(Vector3 initialVelocity, GameObject casterRoot, Transform lineOrigin)
        {
            Phase = FishingNetPhase.Flying;
            _hasLanded = false;
            StatusHint = "Line in the air…";
            _launchPos = transform.position;
            _launchTime = Time.time;

            if (ActiveInWater == this)
                ActiveInWater = null;
            Current = this;

            IgnoreCollisionsWith(casterRoot, true);

            _rb.isKinematic = false;
            _rb.useGravity = true;
            _rb.linearVelocity = initialVelocity;
            _rb.angularVelocity = Vector3.zero;

            EnsurePhases();
            EnsureVisual();
            _visual?.ShowInFlight();
            _visual?.SetLineOrigin(lineOrigin);

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

            StatusHint = "Bite! Hold LMB to reel.";
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
            // Only flying casts time out. A planted bobber stays until cancel / catch.
            if (Phase != FishingNetPhase.Flying)
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

            if (TryHitCreatureCollider(collision.collider))
                return;

            if (IsWater(collision.collider))
            {
                LandInWater(collision);
                return;
            }

            // Don't stick to the player / ground right at the cast origin.
            if (StillInLaunchGrace())
                return;

            if (TryLandInNearbyWater())
                return;

            LandOnDry(collision);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_hasLanded) return;

            if (TryHitCreatureCollider(other))
                return;

            if (!IsWater(other)) return;
            LandInWater(null);
        }

        private bool TryHitCreatureCollider(Collider other)
        {
            if (other == null || StillInLaunchGrace()) return false;
            var hittable = other.GetComponentInParent<INetHittable>();
            if (hittable == null || !hittable.IsNetHittable) return false;

            var result = hittable.OnNetHit(new NetHitInfo(transform.position, NetHitSource.ThrownNet));
            if (result == NetHitResult.Ignored) return false;

            // Caught snake: consume the net. Stunned croc: don't plant on the body — keep flying.
            if (result == NetHitResult.Caught)
            {
                _hasLanded = true;
                CancelMissLifetime();
                Destroy(gameObject);
            }

            return true;
        }

        private void TryHitCreaturesNearPlant()
        {
            var count = Physics.OverlapSphereNonAlloc(
                transform.position,
                creatureHitRadius,
                CreatureNetOverlapBuffer.Colliders,
                ~0,
                QueryTriggerInteraction.Collide);

            for (var i = 0; i < count; i++)
            {
                var c = CreatureNetOverlapBuffer.Colliders[i];
                if (c == null) continue;
                var hittable = c.GetComponentInParent<INetHittable>();
                if (hittable == null || !hittable.IsNetHittable) continue;
                hittable.OnNetHit(new NetHitInfo(transform.position, NetHitSource.ThrownNet));
            }
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

            // Rod bobber only scoops pond loot that is actually under it (rosary, etc.).
            // The quest shiny is hand-net / Interact only — a stray plant must not vacuum it.
            if (NetScoopLoot.TryScoopNear(transform.position, shinyScoopRadius))
            {
                StatusHint = "Scooped!";
                Destroy(gameObject);
                return;
            }

            TryHitCreaturesNearPlant();

            var spot = FishingSpot.FindContaining(transform.position);
            var rodFishNearby = HasRodFishNearby(transform.position, 22f);
            var rodSpot = spot != null && spot.RequiredTool == FishCatchTool.Rod;

            if (spot != null && spot.RequiredTool == FishCatchTool.Net && !rodFishNearby)
            {
                StatusHint = "Net hole — switch to the hand net (2). Esc recast.";
                return;
            }

            if (!rodFishNearby && !rodSpot)
            {
                StatusHint = "No rod fish here. Cast into a rod hole (catfish). Esc recast.";
                return;
            }

            StatusHint = "Fish nearby — wiggle A/D until the bite, then hold LMB.";
            var attract = GetComponent<FishingAttractPhase>();
            if (attract != null)
                attract.BeginAttract();
        }

        private bool TryLandInNearbyWater()
        {
            if (_hasLanded) return false;

            var pos = transform.position;

            const float probe = 1.1f;
            var count = Physics.OverlapSphereNonAlloc(pos, probe, CreatureNetOverlapBuffer.Colliders, ~0,
                QueryTriggerInteraction.Collide);
            for (var i = 0; i < count; i++)
            {
                var c = CreatureNetOverlapBuffer.Colliders[i];
                if (!IsWater(c)) continue;
                var snap = c.ClosestPoint(pos);
                transform.position = snap;
                LandInWater(null);
                return true;
            }

            // Downward probe — water is often a thin trigger plane under the bobber.
            if (Physics.Raycast(pos + Vector3.up * 1.5f, Vector3.down, out var hit, 6f, ~0,
                    QueryTriggerInteraction.Collide) &&
                IsWater(hit.collider))
            {
                transform.position = hit.point;
                LandInWater(null);
                return true;
            }

            return false;
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
            StatusHint = "Missed the water. Esc / RMB recast.";
            CancelMissLifetime();

            if (!stickOnDryLand)
                return;

            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _rb.isKinematic = true;
            EnsureVisual();
            _visual?.ShowPlanted();
            TryHitCreaturesNearPlant();
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

    internal static class CreatureNetOverlapBuffer
    {
        public static readonly Collider[] Colliders = new Collider[32];
    }
}
