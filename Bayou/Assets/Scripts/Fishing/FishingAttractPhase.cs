using System;
using Bayou.Fish;
using Bayou.Input;
using Bayou.Player;
using UnityEngine;
using UnityEngine.Events;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Bayou.Fishing
{
    /// <summary>
    /// Net is planted in water. Fish gradually swim toward it.
    /// Wiggle (A/D) boosts attraction. When a fish reaches bite range → reel phase.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FishingAttractPhase : MonoBehaviour
    {
        [Header("Input")]
#if ENABLE_INPUT_SYSTEM
        [SerializeField] private InputActionReference moveAction;
#endif

        [Header("Attraction")]
        [SerializeField] private float baseAttractRadius = 14f;
        [SerializeField] private float maxAttractRadius = 22f;
        [SerializeField] private float biteRadius = 1.35f;
        [SerializeField] private float passiveAttractPerSecond = 0.04f;
        [SerializeField] private float wiggleAttractPerChange = 0.12f;
        [SerializeField] private float progressDecayPerSecond = 0.03f;
        [SerializeField] private float inputDeadzone = 0.22f;
        [SerializeField] private float minProgressToBite = 0.35f;

        [Header("Events")]
        [SerializeField] private UnityEvent onAttractComplete;

        public event Action AttractComplete;

        public float Progress01 { get; private set; }
        public bool IsActive { get; private set; }
        public float CurrentAttractRadius { get; private set; }

        private int _lastDirection;
        private bool _hasDirection;
        private FishingNetProjectile _net;

        public void BeginAttract()
        {
#if ENABLE_INPUT_SYSTEM
            if (moveAction == null)
            {
                var motor = FindFirstObjectByType<BayouCharacterMotor>();
                if (motor != null)
                    moveAction = motor.MoveAction;
            }
#endif
            _net = GetComponent<FishingNetProjectile>();
            IsActive = true;
            Progress01 = 0.1f;
            CurrentAttractRadius = baseAttractRadius;
            _lastDirection = 0;
            _hasDirection = false;
            enabled = true;
        }

        public void CancelAttract()
        {
            if (!IsActive && !enabled) return;
            IsActive = false;
            Progress01 = 0f;
            enabled = false;
            ClearFishTargets();
        }

        private void Update()
        {
            if (!IsActive) return;

            if (WasCancelPressed())
            {
                CancelAttract();
                Destroy(gameObject);
                return;
            }

#if ENABLE_INPUT_SYSTEM
            var move = BayouInput.ReadMove(moveAction);
#else
            var move = BayouInput.ReadMove(null);
#endif
            var x = move.x;
            var dir = x > inputDeadzone ? 1 : (x < -inputDeadzone ? -1 : 0);
            var dt = Time.unscaledDeltaTime;

            // Passiveive pull while the net sits in the water.
            Progress01 += passiveAttractPerSecond * dt;

            if (dir != 0)
            {
                if (_hasDirection && _lastDirection != 0 && dir != _lastDirection)
                    Progress01 += wiggleAttractPerChange;

                _lastDirection = dir;
                _hasDirection = true;
            }

            Progress01 -= progressDecayPerSecond * dt;
            Progress01 = Mathf.Clamp01(Progress01);

            CurrentAttractRadius = Mathf.Lerp(baseAttractRadius, maxAttractRadius, Progress01);

            var netPos = _net != null ? _net.PlantPosition : transform.position;
            PullFishTowardNet(netPos, CurrentAttractRadius, Progress01);

            if (Progress01 >= minProgressToBite && TryBite(netPos, biteRadius))
                CompleteAttract();
        }

        private void PullFishTowardNet(Vector3 netPos, float radius, float strength01)
        {
            foreach (var fish in FindObjectsByType<BayouFish>(FindObjectsSortMode.None))
            {
                if (fish == null || fish.IsCaught) continue;

                var flat = fish.transform.position - netPos;
                flat.y = 0f;
                if (flat.sqrMagnitude > radius * radius)
                {
                    fish.ClearAttractTarget();
                    continue;
                }

                // Stronger wiggle → stronger swim pull.
                var pull = Mathf.Lerp(0.35f, 1f, strength01);
                fish.SetAttractTarget(netPos, pull);
            }
        }

        private bool TryBite(Vector3 netPos, float radius)
        {
            var radiusSq = radius * radius;
            foreach (var fish in FindObjectsByType<BayouFish>(FindObjectsSortMode.None))
            {
                if (fish == null || fish.IsCaught) continue;
                var flat = fish.transform.position - netPos;
                flat.y = 0f;
                if (flat.sqrMagnitude <= radiusSq)
                    return true;
            }

            return false;
        }

        private void ClearFishTargets()
        {
            foreach (var fish in FindObjectsByType<BayouFish>(FindObjectsSortMode.None))
                fish?.ClearAttractTarget();
        }

        private void CompleteAttract()
        {
            if (!IsActive) return;
            IsActive = false;
            Progress01 = 1f;
            enabled = false;
            ClearFishTargets();
            Bayou.Audio.FishingAudio.Resolve()?.PlayFishOnLine();
            onAttractComplete?.Invoke();
            AttractComplete?.Invoke();
        }

        private static bool WasCancelPressed()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null && (kb.escapeKey.wasPressedThisFrame || kb.qKey.wasPressedThisFrame))
                return true;

            var mouse = Mouse.current;
            return mouse != null && mouse.rightButton.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Q) || Input.GetMouseButtonDown(1);
#endif
        }
    }
}
