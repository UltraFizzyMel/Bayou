#if !ENABLE_INPUT_SYSTEM
#error HandNetAreaController requires the New Input System (ENABLE_INPUT_SYSTEM).
#endif

using System.Collections;
using System.Collections.Generic;
using Bayou.Creatures;
using Bayou.Fish;
using Bayou.Inventory;
using Bayou.Inventory.Shop;
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
        [SerializeField] private float maxReach = 3.2f;
        [Tooltip("Base catch radius. Pulse grows/shrinks around this.")]
        [SerializeField] private float coverageRadius = 1.85f;
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
        [SerializeField] private float missBelowQuality = 0.12f;
        [SerializeField] private Color missRingColor = new(0.85f, 0.22f, 0.18f, 0.9f);
        [SerializeField] private Color goodRingColor = new(0.35f, 0.95f, 0.45f, 0.95f);
        [SerializeField] private Color peakRingColor = new(0.55f, 1f, 0.7f, 1f);

        [Header("Combat melee (when pursued)")]
        [Tooltip("Max distance from the player that a swing can connect.")]
        [SerializeField] private float meleeReach = 3.1f;
        [Tooltip("Forward cone. Creatures inside this angle are hittable.")]
        [SerializeField] private float meleeArcDegrees = 155f;
        [Tooltip("Inside this radius a swing always hits, even beside / behind you.")]
        [SerializeField] private float meleeGuaranteedRadius = 1.45f;
        [SerializeField] private float meleeCooldown = 0.42f;
        [SerializeField] private Color combatRingColor = new(0.95f, 0.25f, 0.2f, 0.9f);
        [Tooltip("How far away a hunting creature can be for combat mode to engage.")]
        [SerializeField] private float pursuitDetectRange = 8.5f;

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
        private float _nextModeCheck;
        private MeleeSweepAttack _sweep;

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
            HideRing();
            HideGhost();
            _ignoreInputUntil = Time.unscaledTime + 0.05f;
        }

        private void OnDisable()
        {
            CancelCharge();
            if (animator != null)
                animator.SetBool("isSwingingNet", false);
            useNetAction?.action?.Disable();
            if (areaRing != null)
                areaRing.enabled = false;
            HideGhost();
        }

        private void Awake()
        {
            EnsureRing();
            _sweep = GetComponent<MeleeSweepAttack>() ?? gameObject.AddComponent<MeleeSweepAttack>();
        }

        private BayouFishingEquipment _equipment;

        private bool IsNetEquipped()
        {
            if (_equipment == null)
                _equipment = GetComponent<BayouFishingEquipment>() ??
                             GetComponentInParent<BayouFishingEquipment>();
            return _equipment != null && _equipment.CurrentItem == BayouHeldItem.Net;
        }

        private void LateUpdate()
        {
            if (!enabled) return;
            if (!IsNetEquipped())
            {
                HideRing();
                HideGhost();
                return;
            }

            if (Bayou.GameplayPause.IsPaused)
            {
                HideRing();
                HideGhost();
                return;
            }

            RefreshMode();

            if (_mode == HandNetMode.Combat)
            {
                HideGhost();
                DrawCombatWedge();
                _hasCenter = true;
                return;
            }

            if (!TryGetNetCenter(out var center))
            {
                _hasCenter = false;
                HideRing();
                HideGhost();
                return;
            }

            _hasCenter = true;
            _lastCenter = center;

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
            if (!IsNetEquipped())
            {
                CancelCharge();
                return;
            }
            if (Time.unscaledTime < _ignoreInputUntil)
                return;
            if (PlayerHealth.BlocksAction ||
                (DialogueManager.GetInstance() != null && DialogueManager.GetInstance().dialogueIsPlaying))
            {
                CancelCharge();
                return;
            }

            if (Bayou.GameplayPause.IsPaused)
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
            if (_charging)
            {
                _mode = HandNetMode.Fishing;
                return;
            }

            if (Time.unscaledTime < _nextModeCheck) return;
            _nextModeCheck = Time.unscaledTime + 0.15f;

            var meleeRange = Mathf.Max(meleeReach + 2.5f, 6.5f);
            var hunterClose = CreatureThreat.IsPlayerPursued(transform, meleeRange);
            var nearSpot = FishingSpot.FindNearby(transform.position, 3f);
            if (nearSpot != null)
            {
                _mode = HandNetMode.Fishing;
                return;
            }

            _mode = hunterClose ? HandNetMode.Combat : HandNetMode.Fishing;
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

            if (heldFor < 0.08f)
            {
                ThrowAtCurrentCircle(0.55f);
                return;
            }

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
            if (_sweep != null && _sweep.IsSwinging)
                return;

            _lastUseTime = Time.time;
            PlaySwingAnim();
            if (_sweep == null)
                _sweep = GetComponent<MeleeSweepAttack>() ?? gameObject.AddComponent<MeleeSweepAttack>();
            _sweep.TryPlay(NetHitSource.MeleeNet, meleeReach, meleeArcDegrees, meleeGuaranteedRadius);
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
                animator.SetBool("isSwingingNet", false);
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
            var origin = netOrigin != null ? netOrigin.position : transform.position + Vector3.up * 0.1f;
            var flat = GetFlatForward();
            center = origin + flat * Mathf.Max(0.6f, maxReach);

            var spot = FishingSpot.FindContaining(center)
                       ?? FishingSpot.FindContaining(origin)
                       ?? FishingSpot.FindNearby(origin, 2.4f);
            center.y = spot != null ? spot.RingSurfaceY : origin.y + 0.03f;
            return true;
        }

        private Vector3 GetFlatForward() => BayouFacing.GetCardinalForward8(transform);

        private static Vector3 GetAimForward(Transform t)
        {
            if (t == null) return Vector3.forward;
            var fwd = t.forward;
            fwd.y = 0f;
            return fwd.sqrMagnitude < 1e-6f ? Vector3.forward : fwd.normalized;
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
                hittable.OnNetHit(new NetHitInfo(center, source, source is NetHitSource.MeleeNet or NetHitSource.MeleeRod ? 1f : 0f));
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

            BayouFish best = null;
            var bestSq = float.MaxValue;
            var seen = BayouFishNetOverlapBuffer.SeenFish;
            seen.Clear();

            for (var i = 0; i < count; i++)
            {
                var c = BayouFishNetOverlapBuffer.Colliders[i];
                if (c == null) continue;
                var fish = c.GetComponentInParent<BayouFish>();
                if (fish == null || fish.IsCaught || !fish.CanCatchWith(FishCatchTool.Net))
                    continue;
                if (!seen.Add(fish))
                    continue;

                var d = fish.transform.position - center;
                d.y = 0f;
                var sq = d.sqrMagnitude;
                if (sq > radius * radius) continue;
                if (sq >= bestSq) continue;
                bestSq = sq;
                best = fish;
            }

            best?.TryCatchFromNet(center, radius);
        }

        private void PlaySwingAnim()
        {
            if (animator == null)
                animator = GetComponentInChildren<Animator>();
            if (animator == null)
                return;

            animator.SetBool("isSwingingNet", true);
            if (_swingRoutine != null)
                StopCoroutine(_swingRoutine);
            _swingRoutine = StartCoroutine(ClearSwingFlag());
        }

        private IEnumerator ClearSwingFlag()
        {
            yield return new WaitForSeconds(0.28f);
            if (animator != null)
                animator.SetBool("isSwingingNet", false);
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
            areaRing.enabled = false;
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

        private void DrawCombatWedge()
        {
            if (areaRing == null) return;
            areaRing.enabled = true;

            var origin = transform.position;
            origin.y += 0.04f;
            var swinging = _sweep != null && _sweep.IsSwinging;
            var forward = swinging ? _sweep.LockedForward : GetFlatForward();
            var range = Mathf.Max(0.6f, meleeReach);
            var half = Mathf.Clamp(meleeArcDegrees, 20f, 180f) * 0.5f;
            var n = Mathf.Clamp(ringSegments, 10, 48);

            areaRing.loop = false;
            var color = combatRingColor;
            if (swinging)
                color = Color.Lerp(combatRingColor, Color.white, 0.45f);
            if (areaRing.material != null)
                areaRing.material.color = color;
            areaRing.startColor = color;
            areaRing.endColor = color;
            areaRing.startWidth = swinging ? 0.09f : 0.07f;
            areaRing.endWidth = swinging ? 0.09f : 0.07f;

            // Full wedge, plus a hotter blade tick at the current sweep yaw.
            var extra = swinging ? 3 : 0;
            areaRing.positionCount = n + 3 + extra;
            areaRing.SetPosition(0, origin);

            for (var i = 0; i <= n; i++)
            {
                var t = i / (float)n;
                var yaw = Mathf.Lerp(-half, half, t);
                var dir = Quaternion.AngleAxis(yaw, Vector3.up) * forward;
                areaRing.SetPosition(i + 1, origin + dir * range);
            }

            areaRing.SetPosition(n + 2, origin);

            if (swinging)
            {
                var blade = Quaternion.AngleAxis(_sweep.CurrentYawDegrees, Vector3.up) * forward;
                var tip = origin + blade * range;
                areaRing.SetPosition(n + 3, origin);
                areaRing.SetPosition(n + 4, tip);
                areaRing.SetPosition(n + 5, origin);
            }
        }

        private void DrawRing(Vector3 center, float radius, Color color, float width)
        {
            if (areaRing == null) return;
            areaRing.enabled = true;
            areaRing.loop = true;
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
                lr.SetPosition(i, new Vector3(x, center.y, z));
            }
        }
    }

    internal static class BayouFishNetOverlapBuffer
    {
        public static readonly Collider[] Colliders = new Collider[32];
        public static readonly HashSet<BayouFish> SeenFish = new(16);
    }
}
