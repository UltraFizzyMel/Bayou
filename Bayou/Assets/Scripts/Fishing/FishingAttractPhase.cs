using System;
using Bayou.Input;
using UnityEngine;
using UnityEngine.Events;

namespace Bayou.Fishing
{
    /// <summary>
    /// Part 2: after the net lands in water, player "wiggles" (alternates horizontal input) to attract fish.
    /// Part 3 (reeling) will hook into <see cref="onAttractComplete"/> later.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FishingAttractPhase : MonoBehaviour
    {
        [Header("Input")]
        [Tooltip("Same Move Vector2 as character motor — horizontal axis used for wiggle detection.")]
#if ENABLE_INPUT_SYSTEM
        [SerializeField] private UnityEngine.InputSystem.InputActionReference moveAction;
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

        // Do not Enable/Disable moveAction here — it is usually shared with BayouCharacterMotor.

        /// <summary>Called by <see cref="FishingNetProjectile"/> when the net settles in water.</summary>
        public void BeginAttract()
        {
            IsActive = true;
            Progress01 = 0f;
            _lastDirection = 0;
            _hasDirection = false;
            enabled = true;
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
                        CompleteAttract();
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
            onAttractComplete?.Invoke();
            AttractComplete?.Invoke();
        }
    }
}
