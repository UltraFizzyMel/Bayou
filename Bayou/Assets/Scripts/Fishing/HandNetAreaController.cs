#if !ENABLE_INPUT_SYSTEM
#error HandNetAreaController requires the New Input System (ENABLE_INPUT_SYSTEM).
#endif

using Bayou.Fish;
using Bayou.Quests;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Bayou.Fishing
{
    /// <summary>
    /// Short-range hand net: shows coverage on the ground/water and on use overlaps fish in that area.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HandNetAreaController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform aimTransform;
        [SerializeField] private Transform netOrigin;

        [Header("Reach & coverage")]
        [Tooltip("Max horizontal distance from player to net center (short throw).")]
        [SerializeField] private float maxReach = 2.8f;

        [Tooltip("Radius of the catch / preview circle on the surface.")]
        [SerializeField] private float coverageRadius = 1.6f;

        [SerializeField] private LayerMask surfaceMask = ~0;

        [Tooltip("Layers that contain fish colliders (prefer a dedicated Fish layer). Default: all layers for prototyping.")]
        [SerializeField] private LayerMask fishMask = ~0;

        [Header("Input")]
        [SerializeField] private InputActionReference useNetAction;

        [Header("Timing")]
        [SerializeField] private float useCooldown = 0.55f;

        [Header("Preview")]
        [SerializeField] private LineRenderer areaRing;
        [SerializeField] private bool autoCreateAreaRing = true;
        [SerializeField] private int ringSegments = 28;
        [SerializeField] private Color ringColor = new(0.95f, 0.75f, 0.15f, 0.85f);

        private float _lastUseTime = -999f;
        private Vector3 _lastCenter;
        private bool _hasCenter;

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

            if (!WasUsePressed())
                return;

            if (Time.time - _lastUseTime < useCooldown)
                return;

            if (!_hasCenter || !TryGetNetCenter(out var center))
                return;

            _lastUseTime = Time.time;
            Bayou.Audio.FishingAudio.Resolve()?.PlayHandNetScoop();

            // One-time pond loot (shiny, rosary, etc.) then fish.
            if (PondShinyCollectible.TryScoopNear(center, coverageRadius))
                return;
            if (NetScoopLoot.TryScoopNear(center, coverageRadius))
                return;

            TryCatchFishInArea(center, coverageRadius);
        }

        private bool WasUsePressed()
        {
            var act = useNetAction?.action;
            if (act != null && act.WasPressedThisFrame())
                return true;

            // Same Cast / LMB as the rod when no dedicated scoop action is wired.
            var mouse = Mouse.current;
            return mouse != null && mouse.leftButton.wasPressedThisFrame;
        }

        private bool TryGetNetCenter(out Vector3 center)
        {
            center = default;
            var origin = netOrigin != null ? netOrigin.position : transform.position + Vector3.up * 0.1f;
            var flat = GetFlatForward();
            var horizontal = origin + flat * maxReach;

            if (Physics.Raycast(horizontal + Vector3.up * 4f, Vector3.down, out var hit, 12f, surfaceMask,
                    QueryTriggerInteraction.Collide))
            {
                center = hit.point;
                return true;
            }

            center = new Vector3(horizontal.x, origin.y, horizontal.z);
            return true;
        }

        private Vector3 GetFlatForward()
        {
            var aim = aimTransform != null ? aimTransform : transform;
            var f = aim.forward;
            f.y = 0f;
            if (f.sqrMagnitude < 0.0001f) f = transform.forward;
            return f.normalized;
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
            var shader =
                Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Sprites/Default");
            var mat = new Material(shader);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", ringColor);
            else if (mat.HasProperty("_Color")) mat.SetColor("_Color", ringColor);
            areaRing.material = mat;
        }

        private void HideRing()
        {
            if (areaRing == null) return;
            areaRing.positionCount = 0;
        }

        private void DrawRing(Vector3 center)
        {
            if (areaRing == null) return;

            var n = Mathf.Clamp(ringSegments, 8, 64);
            areaRing.positionCount = n;

            for (var i = 0; i < n; i++)
            {
                var t = (i / (float)n) * Mathf.PI * 2f;
                var x = center.x + Mathf.Cos(t) * coverageRadius;
                var z = center.z + Mathf.Sin(t) * coverageRadius;
                areaRing.SetPosition(i, new Vector3(x, center.y + 0.03f, z));
            }
        }
    }

    internal static class BayouFishNetOverlapBuffer
    {
        public static readonly Collider[] Colliders = new Collider[32];
    }
}
