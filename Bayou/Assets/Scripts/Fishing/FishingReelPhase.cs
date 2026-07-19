#if !ENABLE_INPUT_SYSTEM
#error FishingReelPhase requires the New Input System (ENABLE_INPUT_SYSTEM).
#endif

using Bayou.Fish;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Bayou.Fishing
{
    /// <summary>
    /// Part 3: after attract completes, hold Cast (or mash) to reel the fish in. Escape / RMB cancels.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FishingReelPhase : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField] private InputActionReference castHoldAction;

        [Header("Reel")]
        [SerializeField] private float reelSeconds = 1.8f;
        [SerializeField] private float catchRadius = 5f;
        [SerializeField] private LayerMask fishMask = ~0;

        public bool IsActive { get; private set; }
        public float Progress01 { get; private set; }

        private FishingNetProjectile _projectile;

        private void Awake()
        {
            _projectile = GetComponent<FishingNetProjectile>();
            enabled = false;
        }

        public void BeginReel()
        {
            IsActive = true;
            Progress01 = 0f;
            enabled = true;
            Bayou.Audio.FishingAudio.Resolve()?.StartReelingLoop();
        }

        public void CancelReel()
        {
            if (!IsActive && !enabled) return;
            Finish(success: false);
        }

        private void Update()
        {
            if (!IsActive) return;

            if (WasCancelPressed())
            {
                CancelReel();
                return;
            }

            var dt = Time.unscaledDeltaTime;
            if (IsReelInputHeld())
                Progress01 += dt / Mathf.Max(0.2f, reelSeconds);
            else
                Progress01 -= dt * 0.35f;

            Progress01 = Mathf.Clamp01(Progress01);

            if (Progress01 >= 1f)
                Finish(success: true);
        }

        private void Finish(bool success)
        {
            IsActive = false;
            enabled = false;
            Bayou.Audio.FishingAudio.Resolve()?.StopReelingLoop();

            if (success)
                CatchNearbyFish();

            Destroy(gameObject);
        }

        private void CatchNearbyFish()
        {
            var center = transform.position;
            var caughtAny = false;

            var count = Physics.OverlapSphereNonAlloc(
                center,
                catchRadius,
                OverlapBuffer.Colliders,
                fishMask,
                QueryTriggerInteraction.Collide);

            for (var i = 0; i < count; i++)
            {
                var col = OverlapBuffer.Colliders[i];
                if (col == null) continue;
                var fish = col.GetComponentInParent<BayouFish>();
                if (fish == null || fish.IsCaught) continue;
                fish.Catch();
                caughtAny = true;
            }

            // Fallback: fish often lack reliable colliders / layers in early setups.
            if (!caughtAny)
            {
                var radiusSq = catchRadius * catchRadius;
                foreach (var fish in Object.FindObjectsByType<BayouFish>(FindObjectsSortMode.None))
                {
                    if (fish == null || fish.IsCaught) continue;
                    var delta = fish.transform.position - center;
                    delta.y = 0f;
                    if (delta.sqrMagnitude <= radiusSq)
                        fish.Catch();
                }
            }
        }

        private bool IsReelInputHeld()
        {
            var a = castHoldAction?.action;
            if (a != null && a.IsPressed())
                return true;

            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.isPressed)
                return true;

            return false;
        }

        private static bool WasCancelPressed()
        {
            var kb = Keyboard.current;
            if (kb != null && (kb.escapeKey.wasPressedThisFrame || kb.qKey.wasPressedThisFrame))
                return true;

            var mouse = Mouse.current;
            return mouse != null && mouse.rightButton.wasPressedThisFrame;
        }

        private static class OverlapBuffer
        {
            public static readonly Collider[] Colliders = new Collider[32];
        }
    }
}
