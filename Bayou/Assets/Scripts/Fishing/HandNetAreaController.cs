#if !ENABLE_INPUT_SYSTEM
#error HandNetAreaController requires the New Input System (ENABLE_INPUT_SYSTEM).
#endif

using System.Collections;
using Bayou.Creatures;
using Bayou.Fish;
using Bayou.Inventory;
using Bayou.Player;
using Bayou.Quests;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Bayou.Fishing
{
    /// <summary>Hand-net behavior: fishing scoop when safe, melee when pursued.</summary>
    public enum HandNetMode
    {
        Fishing,
        Combat
    }

    /// <summary>
    /// Held net. Fishing: hold to wind up (circle pulses), release to throw.
    /// A release near the outer peak scoops a bigger area; a release at the trough misses.
    /// Combat: tap to swing.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HandNetAreaController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform aimTransform;
        [SerializeField] private Transform netOrigin;

        [Header("Fishing scoop")]
        [Tooltip("Max horizontal distance from player to net center (short throw).")]
        [SerializeField] private float maxReach = 2.8f;
        [Tooltip("Base catch radius. Pulse grows/shrinks around this.")]
        [SerializeField] private float coverageRadius = 1.6f;
        [SerializeField] private float fishingCooldown = 0.55f;
        [SerializeField] private Color fishingRingColor = new(0.95f, 0.75f, 0.15f, 0.85f);

        [Header("Hold-to-throw pulse")]
        [Tooltip("Smallest circle while charging (miss zone).")]
        [SerializeField] private float pulseMinScale = 0.35f;
        [Tooltip("Largest circle while charging (best throw).")]
        [SerializeField] private float pulseMaxScale = 1.7f;
        [Tooltip("Seconds for one in-and-out pulse.")]
        [SerializeField] private float pulseCycleSeconds = 1.35f;
        [Tooltip("Release quality below this (0 = smallest, 1 = largest) is a missed throw.")]
        [Range(0.05f, 0.8f)]
        [SerializeField] private float missBelowQuality = 0.42f;
        [SerializeField] private Color missRingColor = new(0.85f, 0.22f, 0.18f, 0.9f);
        [SerializeField] private Color goodRingColor = new(0.35f, 0.95f, 0.45f, 0.95f);
        [SerializeField] private Color peakRingColor = new(0.55f, 1f, 0.7f, 1f);

        [Header("Combat melee (when pursued)")]
        [SerializeField] private float meleeReach = 2.1f;
        [SerializeField] private float meleeRadius = 1.35f;
        [SerializeField] private float meleeCooldown = 0.35f;
        [SerializeField] private Color combatRingColor = new(0.95f, 0.25f, 0.2f, 0.9f);
        [Tooltip("How far away a hunting creature can be for combat mode to engage.")]
        [SerializeField] private float pursuitDetectRange = 45f;

        [Header("Shared")]
        [SerializeField] private LayerMask surfaceMask = ~0;
        [SerializeField] private LayerMask fishMask = ~0;
        [SerializeField] private InputActionReference useNetAction;
        [SerializeField] private LineRenderer areaRing;
        [SerializeField] private LineRenderer peakGhostRing;
        [SerializeField] private bool autoCreateAreaRing = true;
        [SerializeField] private int ringSegments = 28;

        public Animator animator;

        private float _lastUseTime = -999f;
        private Vector3 _lastCenter;
        private bool _hasCenter;
        private HandNetMode _mode = HandNetMode.Fishing;
        private bool _charging;
        private float _chargeStartTime;
        private float _displayRadius;
        private Coroutine _swingRoutine;
        private float _ignoreInputUntil;

        public HandNetMode Mode => _mode;
        public bool IsCombatMode => _mode == HandNetMode.Combat;
        public bool IsCharging => _charging;
        public float Pulse01 { get; private set; }

        private void Reset()
        {
            netOrigin = transform;
            aimTransform = Camera.main != null ? Camera.main.transform : null;
        }

        private void OnEnable()
        {
            useNetAction?.action?.Enable();
            EnsureRing();
            if (areaRing != null)
                areaRing.enabled = true;
            _ignoreInputUntil = Time.unscaledTime + 1f;
        }

        private void OnDisable()
        {
            CancelCharge();
            useNetAction?.action?.Disable();
            if (areaRing != null)
                areaRing.enabled = false;
            HideGhost();
        }

        private void Awake()
        {
            EnsureRing();
        }

        private void LateUpdate()
        {
            if (!enabled) return;

            RefreshMode();

            if (!TryGetNetCenter(out var center))
            {
                _hasCenter = false;
                HideRing();
                HideGhost();
                return;
            }

            _hasCenter = true;
            _lastCenter = center;

            if (_mode == HandNetMode.Combat)
            {
                HideGhost();
                DrawRing(center, meleeRadius, combatRingColor, 0.07f);
                return;
            }

            if (_charging)
            {
                var quality = SamplePulse01();
                Pulse01 = quality;
                _displayRadius = PulseRadius(quality);
                DrawRing(center, _displayRadius, PulseColor(quality), Mathf.Lerp(0.05f, 0.11f, quality));
                DrawGhost(center, PulseRadius(1f), new Color(1f, 1f, 1f, 0.28f));
            }
            else
            {
                Pulse01 = 0f;
                _displayRadius = coverageRadius;
                HideGhost();
                DrawRing(center, coverageRadius, fishingRingColor, 0.06f);
            }
        }

        private void Update()
        {
            if (!enabled) return;
            if (Time.unscaledTime < _ignoreInputUntil)
                return;
            if (DialogueManager.GetInstance() != null && DialogueManager.GetInstance().dialogueIsPlaying)
            {
                CancelCharge();
                return;
            }

            if (InventoryDisplayUI.Active != null && InventoryDisplayUI.Active.IsOpen)
            {
                CancelCharge();
                return;
            }

            RefreshMode();

            if (_mode == HandNetMode.Combat)
            {
                CancelCharge();
                TryCombatSwing();
                return;
            }

            UpdateFishingCharge();
        }

        private void RefreshMode()
        {
            var pursued = CreatureThreat.IsPlayerPursued(transform, pursuitDetectRange);
            _mode = pursued ? HandNetMode.Combat : HandNetMode.Fishing;
        }

        private void UpdateFishingCharge()
        {
            if (WasCancelPressed())
            {
                CancelCharge();
                return;
            }

            var held = IsUseHeld();
            if (held)
            {
                if (!_charging)
                {
                    if (Time.time - _lastUseTime < fishingCooldown)
                        return;

                    _charging = true;
                    _chargeStartTime = Time.time;
                    Pulse01 = 0f;
                }

                return;
            }

            if (!_charging)
                return;

            var heldFor = Time.time - _chargeStartTime;
            var quality = SamplePulse01();
            _charging = false;
            Pulse01 = 0f;

            if (heldFor < 0.15f)
                return;

            if (quality < missBelowQuality)
            {
                PlayMiss();
                return;
            }

            ThrowAtCurrentCircle(quality);
        }

        private void TryCombatSwing()
        {
            if (!WasUsePressedThisFrame())
                return;
            if (Time.time - _lastUseTime < meleeCooldown)
                return;
            if (!_hasCenter || !TryGetNetCenter(out var center))
                return;

            _lastUseTime = Time.time;
            PlaySwingAnim();
            Bayou.Audio.FishingAudio.Resolve()?.PlayHandNetScoop();
            TryMeleeSwing(center, meleeRadius);
        }

        private void ThrowAtCurrentCircle(float quality)
        {
            if (!_hasCenter || !TryGetNetCenter(out var center))
                return;

            var radius = PulseRadius(quality);
            _lastUseTime = Time.time;
            PlaySwingAnim();
            Bayou.Audio.FishingAudio.Resolve()?.PlayThrowNet();

            if (PondShinyCollectible.TryScoopNear(center, radius))
                return;
            if (NetScoopLoot.TryScoopNear(center, radius))
                return;
            if (TryHitCreaturesInArea(center, radius, NetHitSource.HandNet))
                return;

            TryCatchFishInArea(center, radius);
        }

        private void PlayMiss()
        {
            _lastUseTime = Time.time;
            if (animator != null)
                animator.SetBool("isSwinging", false);
        }

        private void CancelCharge()
        {
            _charging = false;
            Pulse01 = 0f;
        }

        private float SamplePulse01()
        {
            var cycle = Mathf.Max(0.2f, pulseCycleSeconds);
            // 0 = smallest (trough), 1 = largest (peak).
            return Mathf.PingPong((Time.time - _chargeStartTime) * (2f / cycle), 1f);
        }

        private float PulseRadius(float quality01)
        {
            var min = Mathf.Max(0.15f, coverageRadius * pulseMinScale);
            var max = Mathf.Max(min + 0.1f, coverageRadius * pulseMaxScale);
            return Mathf.Lerp(min, max, Mathf.Clamp01(quality01));
        }

        private Color PulseColor(float quality01)
        {
            if (quality01 < missBelowQuality)
                return Color.Lerp(missRingColor, fishingRingColor, quality01 / Mathf.Max(0.01f, missBelowQuality));

            var t = Mathf.InverseLerp(missBelowQuality, 1f, quality01);
            return Color.Lerp(goodRingColor, peakRingColor, t);
        }

        private bool IsUseHeld()
        {
            var act = useNetAction?.action;
            if (act != null && act.IsPressed())
                return true;

            var mouse = Mouse.current;
            return mouse != null && mouse.leftButton.isPressed;
        }

        private bool WasUsePressedThisFrame()
        {
            var act = useNetAction?.action;
            if (act != null && act.WasPressedThisFrame())
                return true;

            var mouse = Mouse.current;
            return mouse != null && mouse.leftButton.wasPressedThisFrame;
        }

        private static bool WasCancelPressed()
        {
            var kb = Keyboard.current;
            if (kb != null && (kb.escapeKey.wasPressedThisFrame || kb.qKey.wasPressedThisFrame))
                return true;

            var mouse = Mouse.current;
            return mouse != null && mouse.rightButton.wasPressedThisFrame;
        }

        private bool TryGetNetCenter(out Vector3 center)
        {
            center = default;
            var origin = netOrigin != null ? netOrigin.position : transform.position + Vector3.up * 0.1f;
            var flat = GetFlatForward();
            var reach = _mode == HandNetMode.Combat ? meleeReach : maxReach;
            var horizontal = origin + flat * reach;

            if (Physics.Raycast(horizontal + Vector3.up * 4f, Vector3.down, out var hit, 12f, surfaceMask,
                    QueryTriggerInteraction.Collide))
            {
                center = hit.point;
                return true;
            }

            center = new Vector3(horizontal.x, origin.y, horizontal.z);
            return true;
        }

        private Vector3 GetFlatForward() => BayouFacing.GetCardinalForward8(transform);

        private void TryMeleeSwing(Vector3 center, float radius)
        {
            TryHitCreaturesInArea(center, radius, NetHitSource.MeleeNet);
        }

        private bool TryHitCreaturesInArea(Vector3 center, float radius, NetHitSource source)
        {
            var count = Physics.OverlapSphereNonAlloc(
                center,
                radius,
                BayouFishNetOverlapBuffer.Colliders,
                fishMask,
                QueryTriggerInteraction.Collide);

            var hitAny = false;
            for (var i = 0; i < count; i++)
            {
                var c = BayouFishNetOverlapBuffer.Colliders[i];
                if (c == null) continue;
                var hittable = c.GetComponentInParent<INetHittable>();
                if (hittable == null || !hittable.IsNetHittable) continue;
                hittable.OnNetHit(new NetHitInfo(center, source));
                hitAny = true;
            }

            return hitAny;
        }

        private void TryCatchFishInArea(Vector3 center, float radius)
        {
            var count = Physics.OverlapSphereNonAlloc(
                center,
                radius,
                BayouFishNetOverlapBuffer.Colliders,
                fishMask,
                QueryTriggerInteraction.Collide);

            for (var i = 0; i < count; i++)
            {
                var c = BayouFishNetOverlapBuffer.Colliders[i];
                if (c == null) continue;
                var fish = c.GetComponentInParent<BayouFish>();
                if (fish != null)
                    fish.TryCatchFromNet(center, radius);
            }
        }

        private void PlaySwingAnim()
        {
            if (animator == null)
                return;

            animator.SetBool("isSwinging", true);
            if (_swingRoutine != null)
                StopCoroutine(_swingRoutine);
            _swingRoutine = StartCoroutine(ClearSwingFlag());
        }

        private IEnumerator ClearSwingFlag()
        {
            yield return new WaitForSeconds(0.28f);
            if (animator != null)
                animator.SetBool("isSwinging", false);
            _swingRoutine = null;
        }

        private void EnsureRing()
        {
            if (!autoCreateAreaRing)
                return;

            if (areaRing == null)
            {
                var go = new GameObject("HandNetAreaRing");
                go.transform.SetParent(transform, false);
                areaRing = go.AddComponent<LineRenderer>();
                SetupRing(areaRing, fishingRingColor, 0.06f);
            }

            if (peakGhostRing == null)
            {
                var go = new GameObject("HandNetPeakGhost");
                go.transform.SetParent(transform, false);
                peakGhostRing = go.AddComponent<LineRenderer>();
                SetupRing(peakGhostRing, new Color(1f, 1f, 1f, 0.25f), 0.03f);
                peakGhostRing.enabled = false;
            }
        }

        private static void SetupRing(LineRenderer lr, Color color, float width)
        {
            lr.loop = true;
            lr.useWorldSpace = true;
            lr.startWidth = width;
            lr.endWidth = width;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.material = Bayou.Rendering.BayouShaderUtil.CreateUnlitColor(color);
        }

        private void HideRing()
        {
            if (areaRing == null) return;
            areaRing.positionCount = 0;
        }

        private void HideGhost()
        {
            if (peakGhostRing == null) return;
            peakGhostRing.enabled = false;
            peakGhostRing.positionCount = 0;
        }

        private void DrawGhost(Vector3 center, float radius, Color color)
        {
            if (peakGhostRing == null) return;
            peakGhostRing.enabled = true;
            WriteCircle(peakGhostRing, center, radius, color, 0.03f);
        }

        private void DrawRing(Vector3 center, float radius, Color color, float width)
        {
            if (areaRing == null) return;
            WriteCircle(areaRing, center, radius, color, width);
        }

        private void WriteCircle(LineRenderer lr, Vector3 center, float radius, Color color, float width)
        {
            if (lr.material != null)
                lr.material.color = color;
            lr.startColor = color;
            lr.endColor = color;
            lr.startWidth = width;
            lr.endWidth = width;

            var n = Mathf.Clamp(ringSegments, 8, 64);
            lr.positionCount = n;

            for (var i = 0; i < n; i++)
            {
                var t = (i / (float)n) * Mathf.PI * 2f;
                var x = center.x + Mathf.Cos(t) * radius;
                var z = center.z + Mathf.Sin(t) * radius;
                lr.SetPosition(i, new Vector3(x, center.y + 0.03f, z));
            }
        }
    }

    internal static class BayouFishNetOverlapBuffer
    {
        public static readonly Collider[] Colliders = new Collider[32];
    }
}
