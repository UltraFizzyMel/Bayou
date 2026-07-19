using System;
using Bayou.Input;
using UnityEngine;
using UnityEngine.Events;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Bayou.Fishing
{
    /// <summary>
    /// Part 2: after the net lands in water, player "wiggles" (alternates horizontal input) to attract fish.
    /// On complete, <see cref="FishingReelPhase"/> takes over (Part 3).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FishingAttractPhase : MonoBehaviour
    {
        [Header("Input")]
        [Tooltip("Same Move Vector2 as character motor — horizontal axis used for wiggle detection.")]
#if ENABLE_INPUT_SYSTEM
        [SerializeField] private InputActionReference moveAction;
#endif

        [Header("Difficulty")]
        [SerializeField] private int directionChangesRequired = 12;
        [SerializeField] private float inputDeadzone = 0.22f;
        [SerializeField] private float progressDecayPerSecond = 0.12f;

        [Header("Events")]
        [SerializeField] private UnityEvent onAttractComplete;

        /// <summary>Fires once when wiggle meter fills (fish bites — ready for reeling).</summary>
        public event Action AttractComplete;

        public float Progress01 { get; private set; }
        public bool IsActive { get; private set; }

        private int _lastDirection;
        private bool _hasDirection;

#if ENABLE_INPUT_SYSTEM
        public void SetMoveAction(InputActionReference action) => moveAction = action;
#endif

        /// <summary>Called by <see cref="FishingNetProjectile"/> when the net settles in water.</summary>
        public void BeginAttract()
        {
            IsActive = true;
            Progress01 = 0f;
            _lastDirection = 0;
            _hasDirection = false;
            enabled = true;
            FishingActivity.SetBusy(true);
        }

        private void OnDisable()
        {
            if (IsActive)
            {
                IsActive = false;
                // Reel phase may still be busy; only clear if reel is not running.
                var reel = GetComponent<FishingReelPhase>();
                if (reel == null || !reel.IsActive)
                    FishingActivity.SetBusy(false);
            }
        }

        private void Update()
        {
            if (!IsActive) return;

            var move =
#if ENABLE_INPUT_SYSTEM
                BayouInput.ReadMove(moveAction);
#else
                BayouInput.ReadMove(null);
#endif

            var x = move.x;
            var dir = x > inputDeadzone ? 1 : (x < -inputDeadzone ? -1 : 0);

            if (dir != 0)
            {
                if (_hasDirection && _lastDirection != 0 && dir != _lastDirection)
                {
                    Progress01 += 1f / Mathf.Max(1, directionChangesRequired);
                    if (Progress01 >= 1f)
                    {
                        CompleteAttract();
                        return;
                    }
                }

                _lastDirection = dir;
                _hasDirection = true;
            }

            Progress01 -= progressDecayPerSecond * Time.deltaTime;
            Progress01 = Mathf.Clamp01(Progress01);
        }

        private void CompleteAttract()
        {
            if (!IsActive) return;
            IsActive = false;
            Progress01 = 1f;
            enabled = false;
            // Keep FishingActivity busy — reel phase begins immediately after.
            onAttractComplete?.Invoke();
            AttractComplete?.Invoke();
        }
    }
}
