#if !ENABLE_INPUT_SYSTEM
#error HandNetAreaController requires the New Input System (ENABLE_INPUT_SYSTEM).
#endif

using Bayou.Creatures;
using Bayou.Fish;
using Bayou.Inventory;
using Bayou.Player;
using Bayou.Quests;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace Bayou.Fishing
{
    /// <summary>Hand-net behavior: fishing scoop when safe, melee when pursued.</summary>
    public enum HandNetMode
    {
        Fishing,
        Combat
    }

    /// <summary>
    /// Short-range hand net. Context switches automatically:
    /// safe → scoop fish/loot; pursued → melee swing against creatures.
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
        [Tooltip("Radius of the catch / preview circle on the surface.")]
        [SerializeField] private float coverageRadius = 1.6f;
        [SerializeField] private float fishingCooldown = 0.55f;
        [SerializeField] private Color fishingRingColor = new(0.95f, 0.75f, 0.15f, 0.85f);

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
        [SerializeField] private bool autoCreateAreaRing = true;
        [SerializeField] private int ringSegments = 28;

        private float _lastUseTime = -999f;
        private Vector3 _lastCenter;
        private bool _hasCenter;
        private HandNetMode _mode = HandNetMode.Fishing;

        public HandNetMode Mode => _mode;
        public bool IsCombatMode => _mode == HandNetMode.Combat;

        public Animator animator;


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
        }

        private void OnDisable()
        {
            useNetAction?.action?.Disable();
            if (areaRing != null)
                areaRing.enabled = false;
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
                return;
            }

            _hasCenter = true;
            _lastCenter = center;
            DrawRing(center);
        }

        private void Update()
        {
            
            if (!enabled) return;
            if (DialogueManager.GetInstance() != null && DialogueManager.GetInstance().dialogueIsPlaying)
                return;
            if (InventoryDisplayUI.Active != null && InventoryDisplayUI.Active.IsOpen)
                return;

            RefreshMode();

            if (!WasUsePressed())
                return;

            var cooldown = _mode == HandNetMode.Combat ? meleeCooldown : fishingCooldown;
            if (Time.time - _lastUseTime < cooldown)
                return;

            if (!_hasCenter || !TryGetNetCenter(out var center))
                return;

            animator.SetBool("isSwinging", true);

            _lastUseTime = Time.time;
            Bayou.Audio.FishingAudio.Resolve()?.PlayHandNetScoop();

            if (_mode == HandNetMode.Combat)
            {
                TryMeleeSwing(center, meleeRadius);
                return;
            }

            // Fishing: loot → creatures (passive catch/stun) → fish.
            if (PondShinyCollectible.TryScoopNear(center, coverageRadius))
                return;
            if (NetScoopLoot.TryScoopNear(center, coverageRadius))
                return;

            if (TryHitCreaturesInArea(center, coverageRadius, NetHitSource.HandNet))
                return;

            TryCatchFishInArea(center, coverageRadius);
        }

        private void RefreshMode()
        {
            var pursued = CreatureThreat.IsPlayerPursued(transform, pursuitDetectRange);
            _mode = pursued ? HandNetMode.Combat : HandNetMode.Fishing;
        }

        private bool WasUsePressed()
        {
            var act = useNetAction?.action;
            if (act != null && act.WasPressedThisFrame())
                return true;

            var mouse = Mouse.current;
            return mouse != null && mouse.leftButton.wasPressedThisFrame;
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
            animator.SetBool("isSwinging", false);
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
            animator.SetBool("isSwinging", false);
        }

        private void EnsureRing()
        {
            if (!autoCreateAreaRing || areaRing != null) return;

            var go = new GameObject("HandNetAreaRing");
            go.transform.SetParent(transform, false);
            areaRing = go.AddComponent<LineRenderer>();
            areaRing.loop = true;
            areaRing.useWorldSpace = true;
            areaRing.startWidth = 0.06f;
            areaRing.endWidth = 0.06f;
            areaRing.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            areaRing.receiveShadows = false;
            areaRing.material = Bayou.Rendering.BayouShaderUtil.CreateUnlitColor(fishingRingColor);
        }

        private void HideRing()
        {
            if (areaRing == null) return;
            areaRing.positionCount = 0;
        }

        private void DrawRing(Vector3 center)
        {
            if (areaRing == null) return;

            var radius = _mode == HandNetMode.Combat ? meleeRadius : coverageRadius;
            var color = _mode == HandNetMode.Combat ? combatRingColor : fishingRingColor;
            if (areaRing.material != null)
                areaRing.material.color = color;
            areaRing.startColor = color;
            areaRing.endColor = color;

            var n = Mathf.Clamp(ringSegments, 8, 64);
            areaRing.positionCount = n;

            for (var i = 0; i < n; i++)
            {
                var t = (i / (float)n) * Mathf.PI * 2f;
                var x = center.x + Mathf.Cos(t) * radius;
                var z = center.z + Mathf.Sin(t) * radius;
                areaRing.SetPosition(i, new Vector3(x, center.y + 0.03f, z));
            }
        }
    }

    internal static class BayouFishNetOverlapBuffer
    {
        public static readonly Collider[] Colliders = new Collider[32];
    }
}
