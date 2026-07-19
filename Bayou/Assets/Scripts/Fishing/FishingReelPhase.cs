#if !ENABLE_INPUT_SYSTEM
#error FishingReelPhase requires the New Input System (ENABLE_INPUT_SYSTEM).
#endif

using System;
using Bayou.Fish;
using Bayou.Input;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace Bayou.Fishing
{
    /// <summary>
    /// Part 3: after attract completes, hold Move to reel. Nearby fish are pulled in; on fill they are caught.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FishingReelPhase : MonoBehaviour
    {
        [Header("Input")]
        [Tooltip("Same Move Vector2 as character motor — hold stick/keys to reel.")]
        [SerializeField] private InputActionReference moveAction;

        [Header("Difficulty")]
        [SerializeField] private float reelFillPerSecond = 0.4f;
        [SerializeField] private float reelDecayPerSecond = 0.18f;
        [SerializeField] private float inputDeadzone = 0.2f;
        [SerializeField] private float timeoutSeconds = 10f;

        [Header("Catch")]
        [SerializeField] private float attractRadius = 5.5f;
        [SerializeField] private float catchRadius = 2.2f;
        [SerializeField] private float fishPullSpeed = 2.8f;
        [SerializeField] private LayerMask fishMask = ~0;
        [SerializeField] private int maxFishToCatch = 3;

        [Header("Events")]
        [SerializeField] private UnityEvent onReelSuccess;
        [SerializeField] private UnityEvent onReelFail;

        public event Action ReelSuccess;
        public event Action ReelFail;

        public float Progress01 { get; private set; }
        public bool IsActive { get; private set; }

        private float _endTime;
        private readonly BayouFish[] _hooked = new BayouFish[16];
        private int _hookedCount;

        public void SetMoveAction(InputActionReference action) => moveAction = action;

        public void BeginReel()
        {
            IsActive = true;
            Progress01 = 0.15f;
            _endTime = Time.time + Mathf.Max(1f, timeoutSeconds);
            _hookedCount = 0;
            enabled = true;
            FishingActivity.SetBusy(true);
            HookNearbyFish();
        }

        private void OnDisable()
        {
            ReleaseHookedFish();
            if (IsActive)
            {
                IsActive = false;
                FishingActivity.SetBusy(false);
            }
        }

        private void Update()
        {
            if (!IsActive) return;

            if (Time.time >= _endTime)
            {
                FailReel();
                return;
            }

            var move = BayouInput.ReadMove(moveAction);
            var holding = move.sqrMagnitude >= inputDeadzone * inputDeadzone;

            if (holding)
                Progress01 += reelFillPerSecond * Time.deltaTime;
            else
                Progress01 -= reelDecayPerSecond * Time.deltaTime;

            Progress01 = Mathf.Clamp01(Progress01);

            PullHookedFish();

            if (Progress01 >= 1f)
                CompleteReel();
        }

        private void HookNearbyFish()
        {
            var count = Physics.OverlapSphereNonAlloc(
                transform.position,
                attractRadius,
                BayouFishNetOverlapBuffer.Colliders,
                fishMask,
                QueryTriggerInteraction.Collide);

            for (var i = 0; i < count && _hookedCount < _hooked.Length; i++)
            {
                var c = BayouFishNetOverlapBuffer.Colliders[i];
                if (c == null) continue;

                var fish = c.GetComponentInParent<BayouFish>();
                if (fish == null || fish.IsCaught || fish.IsBeingReeled)
                    continue;

                if (!FishingZoneManager.IsInFishingZone(fish.transform.position))
                    continue;

                fish.IsBeingReeled = true;
                _hooked[_hookedCount++] = fish;
            }
        }

        private void PullHookedFish()
        {
            var target = transform.position;
            for (var i = 0; i < _hookedCount; i++)
            {
                var fish = _hooked[i];
                if (fish == null || fish.IsCaught) continue;
                fish.PullToward(target, fishPullSpeed);
            }
        }

        private void CompleteReel()
        {
            if (!IsActive) return;
            IsActive = false;
            Progress01 = 1f;
            enabled = false;

            CatchHookedFish();
            ReleaseHookedFish();
            FishingActivity.SetBusy(false);

            onReelSuccess?.Invoke();
            ReelSuccess?.Invoke();
        }

        private void FailReel()
        {
            if (!IsActive) return;
            IsActive = false;
            enabled = false;

            ReleaseHookedFish();
            FishingActivity.SetBusy(false);

            onReelFail?.Invoke();
            ReelFail?.Invoke();
        }

        private void CatchHookedFish()
        {
            var caught = 0;
            var center = transform.position;
            var radius = Mathf.Max(catchRadius, 0.1f);

            for (var i = 0; i < _hookedCount && caught < maxFishToCatch; i++)
            {
                var fish = _hooked[i];
                if (fish == null || fish.IsCaught) continue;

                var flat = fish.transform.position - center;
                flat.y = 0f;
                if (flat.magnitude > radius) continue;

                fish.Catch();
                caught++;
            }

            // Also scoop anything that swam into the catch radius during the fight.
            if (caught < maxFishToCatch)
            {
                var count = Physics.OverlapSphereNonAlloc(
                    center,
                    radius,
                    BayouFishNetOverlapBuffer.Colliders,
                    fishMask,
                    QueryTriggerInteraction.Collide);

                for (var i = 0; i < count && caught < maxFishToCatch; i++)
                {
                    var c = BayouFishNetOverlapBuffer.Colliders[i];
                    if (c == null) continue;
                    var fish = c.GetComponentInParent<BayouFish>();
                    if (fish == null || fish.IsCaught) continue;
                    fish.TryCatchFromNet(center, radius);
                    if (fish.IsCaught) caught++;
                }
            }
        }

        private void ReleaseHookedFish()
        {
            for (var i = 0; i < _hookedCount; i++)
            {
                if (_hooked[i] != null)
                    _hooked[i].IsBeingReeled = false;
                _hooked[i] = null;
            }

            _hookedCount = 0;
        }
    }
}
