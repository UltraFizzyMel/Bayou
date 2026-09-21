#if !ENABLE_INPUT_SYSTEM
#error BayouFishingEquipment requires the New Input System (ENABLE_INPUT_SYSTEM).
#endif

using Bayou.Creatures;
using Bayou.Inventory;
using Bayou.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace Bayou.Fishing
{
    /// <summary>What the player is currently holding.</summary>
    public enum BayouHeldItem
    {
        None = 0,
        Rod = 1,
        Net = 2,
        Lantern = 3
    }

    // Keep old name so existing scene scripts that reference the type still compile if any.
    public enum BayouFishingTool
    {
        None = BayouHeldItem.None,
        Rod = BayouHeldItem.Rod,
        Net = BayouHeldItem.Net,
        Lantern = BayouHeldItem.Lantern
    }

    /// <summary>
    /// Cycles / selects held item: nothing, rod, net, lantern.
    /// Keys (defaults): Tab cycle · 0 none · 1 rod · 2 net · 3 lantern.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(50)]
    public sealed class BayouFishingEquipment : MonoBehaviour
    {
        [Header("Tools")]
        [FormerlySerializedAs("throwingCaster")]
        [SerializeField] private FishingNetCaster rodCaster;
        [SerializeField] private HandNetAreaController handNet;

        [Header("Held visuals (separate objects)")]
        [SerializeField] private GameObject heldRod;
        [SerializeField] private GameObject heldNet;
        [SerializeField] private GameObject heldLantern;
        [SerializeField] private Transform heldAttachPoint;
        [SerializeField] private bool createPlaceholdersIfMissing = false;

        [Header("Input")]
        [SerializeField] private InputActionReference switchToolAction;
        [SerializeField] private InputActionReference selectNoneAction;
        [SerializeField] private InputActionReference selectRodAction;
        [SerializeField] private InputActionReference selectNetAction;
        [SerializeField] private InputActionReference selectLanternAction;

        [SerializeField] private BayouHeldItem startingItem = BayouHeldItem.None;
        [Tooltip("Rod can only be held after buying Item_FishingRod from Caliste.")]
        [SerializeField] private bool requireRodItem = true;
        [SerializeField] private string fishingRodItemId = "Item_FishingRod";
        [Tooltip("Hand net can only be held after picking up Item_HandNet.")]
        [SerializeField] private bool requireNetItem = true;
        [SerializeField] private string handNetItemId = "Item_HandNet";
        [Tooltip("Lantern can only be held after picking up Item_Lantern.")]
        [SerializeField] private bool requireLanternItem = true;
        [SerializeField] private string lanternItemId = "Item_Lantern";
        [Header("Combat context")]
        [Tooltip("When a creature is actively chasing you, auto-switch to the net (melee).")]
        [SerializeField] private bool autoEquipNetWhenPursued = true;
        [SerializeField] private float pursuitDetectRange = 45f;
        [SerializeField] private Animator animator;

        private static readonly BayouHeldItem[] CycleOrder =
        {
            BayouHeldItem.None,
            BayouHeldItem.Rod,
            BayouHeldItem.Net,
            BayouHeldItem.Lantern
        };

        private HeldLantern _lantern;
        private Light _carryLight;
        private bool _wasPursued;
        private float _nextPursuitCheck;

        public BayouHeldItem CurrentItem { get; private set; } = BayouHeldItem.None;

        /// <summary>Alias for older call sites / HUD.</summary>
        public BayouFishingTool CurrentTool => (BayouFishingTool)CurrentItem;

        public bool IsHolding(BayouHeldItem item) => CurrentItem == item;

        /// <summary>True when a creature is actively hunting the player.</summary>
        public bool IsPursued { get; private set; }

        /// <summary>Hand-net fishing vs combat mode (only meaningful while holding Net).</summary>
        public HandNetMode NetMode =>
            handNet != null ? handNet.Mode :
            (IsPursued ? HandNetMode.Combat : HandNetMode.Fishing);

        private void Reset()
        {
            rodCaster = GetComponent<FishingNetCaster>();
            handNet = GetComponent<HandNetAreaController>();
        }

        private void Awake()
        {
            if (rodCaster == null) rodCaster = GetComponent<FishingNetCaster>();
            if (handNet == null) handNet = GetComponent<HandNetAreaController>();
            if (handNet == null)
                handNet = gameObject.AddComponent<HandNetAreaController>();

            if (GetComponent<FishingInteractionPromptSource>() == null)
                gameObject.AddComponent<FishingInteractionPromptSource>();

            HidePhysicalHeldItems();
            if (animator == null)
                animator = GetComponentInChildren<Animator>();
            if (handNet != null && handNet.animator == null)
                handNet.animator = animator;
            if (rodCaster != null && rodCaster.animator == null)
                rodCaster.animator = animator;
            // Disable tools before the first Update so a Play-mode click cannot auto-cast.
            ApplyItem(startingItem);
        }

        private void OnEnable()
        {
            switchToolAction?.action?.Enable();
            selectNoneAction?.action?.Enable();
            selectRodAction?.action?.Enable();
            selectNetAction?.action?.Enable();
            selectLanternAction?.action?.Enable();
        }

        private void OnDisable()
        {
            switchToolAction?.action?.Disable();
            selectNoneAction?.action?.Disable();
            selectRodAction?.action?.Disable();
            selectNetAction?.action?.Disable();
            selectLanternAction?.action?.Disable();
        }

        public static bool TryResolveHeldItem(string itemId, out BayouHeldItem held)
        {
            held = BayouHeldItem.None;
            if (string.IsNullOrWhiteSpace(itemId)) return false;
            if (itemId.IndexOf("FishingRod", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                itemId.IndexOf("Item_Rod", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                held = BayouHeldItem.Rod;
                return true;
            }

            if (itemId.IndexOf("HandNet", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                string.Equals(itemId, "Item_Net", System.StringComparison.OrdinalIgnoreCase))
            {
                held = BayouHeldItem.Net;
                return true;
            }

            if (itemId.IndexOf("Lantern", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                held = BayouHeldItem.Lantern;
                return true;
            }

            return false;
        }

        public bool TryEquipItemId(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                ApplyItem(BayouHeldItem.None);
                return true;
            }

            if (!TryResolveHeldItem(itemId, out var held))
                return false;
            if (!CanHold(held))
                return false;
            ApplyItem(held);
            return true;
        }

        private void Update()
        {
            UpdatePursuitContext();
            SyncHeldAnimator();

            // The hotwheel owns Tab / 1–4 while it is actually handling input.
            if (EquipmentHotwheel.SuppressLegacyToolKeys)
                return;

            if (WasSelect(selectNoneAction, Key.Digit0, Key.Backquote))
            {
                ApplyItem(BayouHeldItem.None);
                return;
            }

            if (WasSelect(selectRodAction, Key.Digit1))
            {
                if (!CanHold(BayouHeldItem.Rod))
                {
                    Debug.Log("[Equipment] Buy a Fishing Rod from Caliste first.");
                    return;
                }

                ApplyItem(BayouHeldItem.Rod);
                return;
            }

            if (WasSelect(selectNetAction, Key.Digit2))
            {
                if (!CanHold(BayouHeldItem.Net))
                {
                    Debug.Log("[Equipment] Pick up the hand net first.");
                    return;
                }

                ApplyItem(BayouHeldItem.Net);
                return;
            }

            if (WasSelect(selectLanternAction, Key.Digit3))
            {
                if (!CanHold(BayouHeldItem.Lantern))
                {
                    Debug.Log("[Equipment] Find the lantern in the Foggy Marsh first.");
                    return;
                }

                ApplyItem(BayouHeldItem.Lantern);
                return;
            }

            if (WasSwitch())
                CycleNext();
        }

        private void LateUpdate()
        {
            HideHeld(heldRod);
            HideHeld(heldNet);
            HideHeld(heldLantern);
            UpdateCarryLight();
        }

        private void UpdatePursuitContext()
        {
            if (Time.unscaledTime < _nextPursuitCheck) return;
            _nextPursuitCheck = Time.unscaledTime + 0.15f;
            IsPursued = CreatureThreat.IsPlayerPursued(transform, pursuitDetectRange);

            if (!autoEquipNetWhenPursued)
            {
                _wasPursued = IsPursued;
                return;
            }

            // Entering chase: pull out the net for melee unless already holding rod (rod is also melee).
            if (IsPursued && !_wasPursued &&
                CurrentItem != BayouHeldItem.Net &&
                CurrentItem != BayouHeldItem.Rod &&
                CanHold(BayouHeldItem.Net))
            {
                var rodBusy = rodCaster != null &&
                              (rodCaster.Phase != FishingCastPhase.Idle || rodCaster.HasActiveNet);
                if (!rodBusy)
                    ApplyItem(BayouHeldItem.Net);
            }

            _wasPursued = IsPursued;
        }

        public void CycleNext()
        {
            var idx = 0;
            for (var i = 0; i < CycleOrder.Length; i++)
            {
                if (CycleOrder[i] == CurrentItem)
                {
                    idx = i;
                    break;
                }
            }

            for (var step = 1; step <= CycleOrder.Length; step++)
            {
                var next = CycleOrder[(idx + step) % CycleOrder.Length];
                if (CanHold(next))
                {
                    ApplyItem(next);
                    return;
                }
            }
        }

        /// <summary>Prefer <see cref="ApplyItem"/>.</summary>
        public void ApplyTool(BayouFishingTool tool) => ApplyItem((BayouHeldItem)tool);

        public bool CanHold(BayouHeldItem item)
        {
            switch (item)
            {
                case BayouHeldItem.Rod:
                    return !requireRodItem || HasItem(fishingRodItemId);
                case BayouHeldItem.Net:
                    return !requireNetItem || HasItem(handNetItemId);
                case BayouHeldItem.Lantern:
                    return !requireLanternItem || HasItem(lanternItemId);
                default:
                    return true;
            }
        }

        private static bool HasItem(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId)) return true;
            var inv = InventoryController.Instance;
            return inv != null && inv.HasItemsById(itemId, 1);
        }

        public void ApplyItem(BayouHeldItem item)
        {
            if (!CanHold(item))
                item = BayouHeldItem.None;

            // Don't leave the rod mid-cast / while a line is out.
            if (CurrentItem == BayouHeldItem.Rod && item != BayouHeldItem.Rod &&
                rodCaster != null &&
                (rodCaster.Phase != FishingCastPhase.Idle || rodCaster.HasActiveNet))
            {
                return;
            }

            CurrentItem = item;
            HidePhysicalHeldItems();

            if (rodCaster != null && !rodCaster.enabled)
                rodCaster.enabled = true;

            if (handNet != null && !handNet.enabled)
                handNet.enabled = true;

            SyncHeldAnimator();
            UpdateCarryLight();
        }

        /// <summary>
        /// In-hand meshes and hold clips are disabled until that attach is stable.
        /// Tools still work from the hotwheel; the lantern is a body light.
        /// </summary>
        private void SyncHeldAnimator()
        {
            if (animator == null)
                animator = GetComponentInChildren<Animator>();
            if (animator == null) return;

            animator.SetBool("isHoldingRod", false);
            animator.SetBool("isHoldingLantern", false);
        }

        private void HidePhysicalHeldItems()
        {
            HideHeld(heldRod);
            HideHeld(heldNet);
            HideHeld(heldLantern);

            for (var i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                if (child == null) continue;
                var n = child.name;
                if (n.StartsWith("HeldRod", System.StringComparison.OrdinalIgnoreCase) ||
                    n.StartsWith("HeldNet", System.StringComparison.OrdinalIgnoreCase) ||
                    n.StartsWith("HeldLantern", System.StringComparison.OrdinalIgnoreCase))
                    HideHeld(child.gameObject);
            }

            if (animator == null) return;
            var bones = animator.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < bones.Length; i++)
            {
                var t = bones[i];
                if (t == null) continue;
                var n = t.name;
                if (n.StartsWith("HeldRod", System.StringComparison.OrdinalIgnoreCase) ||
                    n.StartsWith("HeldNet", System.StringComparison.OrdinalIgnoreCase) ||
                    n.StartsWith("HeldLantern", System.StringComparison.OrdinalIgnoreCase))
                    HideHeld(t.gameObject);
            }
        }

        private static void HideHeld(GameObject go)
        {
            if (go == null) return;
            if (go.GetComponent<BayouFishingEquipment>() != null) return;
            if (go.GetComponentInParent<EquipmentHotwheel>() != null) return;
            if (go.GetComponent<Canvas>() != null) return;
            if (go.activeSelf)
                go.SetActive(false);
        }

        private void UpdateCarryLight()
        {
            EnsureCarryLight();
            var on = CurrentItem == BayouHeldItem.Lantern;
            if (_carryLight != null)
                _carryLight.enabled = on;
            _lantern?.SetLit(false);
        }

        private void EnsureCarryLight()
        {
            if (_carryLight != null) return;
            var existing = transform.Find("CarryLanternLight");
            var go = existing != null ? existing.gameObject : new GameObject("CarryLanternLight");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0.12f, 1.15f, 0.18f);
            go.transform.localScale = Vector3.one;
            _carryLight = go.GetComponent<Light>() ?? go.AddComponent<Light>();
            _carryLight.type = LightType.Point;
            _carryLight.intensity = 18f;
            _carryLight.range = 9f;
            _carryLight.color = new Color(1f, 0.78f, 0.48f, 1f);
            _carryLight.shadows = LightShadows.None;
            _carryLight.renderMode = LightRenderMode.ForcePixel;
            _carryLight.cullingMask = ~0;
            _carryLight.renderingLayerMask = int.MaxValue;
            _carryLight.enabled = false;
            if (go.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalLightData>() == null)
                go.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalLightData>();
        }

        private static bool IsSwimHoldState(AnimatorStateInfo info) =>
            info.IsName("Armature|Swimming") ||
            info.IsName("Armature|IdleSwimming") ||
            info.IsName("Armature|SwimmingHoldingRod") ||
            info.IsName("Armature|SwimmingHoldingLight");

        private void EnsureHeldVisuals()
        {
            if (animator == null)
                animator = GetComponentInChildren<Animator>();

            var attach = transform;

            heldRod = EnsureHeldProp(heldRod, attach, "HeldRod", CreateRodPlaceholder);
            heldNet = EnsureHeldProp(heldNet, attach, "HeldNet", CreateNetPlaceholder);
            heldLantern = EnsureHeldProp(heldLantern, attach, "HeldLantern", CreateLanternPlaceholder);

            StripPlaceholderColliders(heldRod);
            StripPlaceholderColliders(heldNet);
            StripPlaceholderColliders(heldLantern);
            HideFlatMeshes(heldRod);
            HideFlatMeshes(heldNet);
            HideFlatMeshes(heldLantern);

            if (heldLantern != null)
            {
                _lantern = heldLantern.GetComponent<HeldLantern>();
                if (_lantern == null)
                    _lantern = heldLantern.GetComponentInChildren<HeldLantern>(true);
                if (_lantern == null)
                    _lantern = heldLantern.AddComponent<HeldLantern>();
            }
        }

        private GameObject EnsureHeldProp(
            GameObject existing,
            Transform attach,
            string namePrefix,
            System.Func<Transform, GameObject> create)
        {
            if (existing != null &&
                existing.name.StartsWith(namePrefix, System.StringComparison.OrdinalIgnoreCase) &&
                !NeedsHeldRebuild(existing))
            {
                if (existing.transform.parent != attach)
                    existing.transform.SetParent(attach, false);
                return existing;
            }

            if (existing != null)
            {
                if (existing.transform == transform || existing.transform.IsChildOf(transform))
                    existing.SetActive(false);
            }

            if (!createPlaceholdersIfMissing && existing != null)
                return existing;

            return create(attach);
        }

        private static bool NeedsHeldRebuild(GameObject go)
        {
            if (go == null) return true;
            if (HasFlatMesh(go)) return true;
            var size = MaxWorldSize(go.transform);
            return size > 4f;
        }

        private Transform _cachedHand;
        private Vector3 _heldFollowVel;
        private bool _heldFollowInit;

        private void FollowHeldVisuals()
        {
            if (_cachedHand == null)
                _cachedHand = ResolveHandAttach();
            var hand = _cachedHand != null ? _cachedHand : ResolveHandAttach();
            FollowHeld(heldRod, hand, new Vector3(0.02f, -0.02f, 0.08f), Quaternion.Euler(8f, 90f, 80f), 0.55f,
                CurrentItem == BayouHeldItem.Rod);
            FollowHeld(heldNet, hand, new Vector3(0.04f, 0.01f, 0.1f), Quaternion.Euler(25f, 0f, 0f), 0.4f,
                CurrentItem == BayouHeldItem.Net);
            FollowHeld(heldLantern, hand, new Vector3(0.02f, -0.1f, 0.03f), Quaternion.Euler(0f, 0f, 8f), 0.28f,
                CurrentItem == BayouHeldItem.Lantern);
        }

        private void FollowHeld(GameObject go, Transform hand, Vector3 localPos, Quaternion localRot, float worldScale, bool equipped)
        {
            if (go == null) return;
            var t = go.transform;
            if (!equipped)
            {
                if (go.activeSelf)
                    go.SetActive(false);
                if (t.parent != transform)
                    t.SetParent(transform, false);
                t.localScale = Vector3.one * 0.01f;
                _heldFollowInit = false;
                return;
            }

            if (!go.activeSelf)
                go.SetActive(true);

            var water = GetComponent<Bayou.Player.BayouWaterSensor>();
            var swimming = water != null && water.IsSwimming;
            if (swimming && hand != null && hand != transform)
            {
                if (t.parent != transform)
                    t.SetParent(transform, true);
                var wantPos = hand.TransformPoint(localPos);
                var wantRot = hand.rotation * localRot;
                if (!_heldFollowInit)
                {
                    t.position = wantPos;
                    t.rotation = wantRot;
                    _heldFollowVel = Vector3.zero;
                    _heldFollowInit = true;
                }
                else
                {
                    t.position = Vector3.SmoothDamp(t.position, wantPos, ref _heldFollowVel, 0.12f);
                    t.rotation = Quaternion.Slerp(t.rotation, wantRot, 1f - Mathf.Exp(-10f * Time.deltaTime));
                }

                t.localScale = Vector3.one * worldScale;
                return;
            }

            _heldFollowInit = false;
            var parent = hand != null && hand != transform ? hand : transform;
            if (t.parent != parent)
                t.SetParent(parent, false);

            t.localPosition = parent == transform
                ? new Vector3(0.22f, 0.9f, 0.2f) + localPos
                : localPos;
            t.localRotation = localRot;
            t.localScale = CompensatedLocalScale(t, worldScale);
        }

        private static Vector3 CompensatedLocalScale(Transform t, float worldScale)
        {
            var parent = t.parent;
            if (parent == null)
                return Vector3.one * worldScale;
            var ls = parent.lossyScale;
            // Clamp so a tiny swim-bone scale cannot blow the net into a pond-sized disc.
            var sx = Mathf.Clamp(worldScale / Mathf.Max(0.05f, Mathf.Abs(ls.x)), 0.05f, 1.8f);
            var sy = Mathf.Clamp(worldScale / Mathf.Max(0.05f, Mathf.Abs(ls.y)), 0.05f, 1.8f);
            var sz = Mathf.Clamp(worldScale / Mathf.Max(0.05f, Mathf.Abs(ls.z)), 0.05f, 1.8f);
            return new Vector3(sx, sy, sz);
        }

        private static void StripPlaceholderColliders(GameObject root)
        {
            if (root == null) return;
            var cols = root.GetComponentsInChildren<Collider>(true);
            for (var i = 0; i < cols.Length; i++)
            {
                if (cols[i] != null)
                    Object.Destroy(cols[i]);
            }
        }

        private static void HideFlatMeshes(GameObject root)
        {
            if (root == null) return;
            var filters = root.GetComponentsInChildren<MeshFilter>(true);
            for (var i = 0; i < filters.Length; i++)
            {
                var mf = filters[i];
                if (mf == null || !IsFlatMesh(mf.sharedMesh)) continue;
                var rend = mf.GetComponent<Renderer>();
                if (rend != null)
                    rend.enabled = false;
            }
        }

        private static bool HasFlatMesh(GameObject root)
        {
            if (root == null) return false;
            var filters = root.GetComponentsInChildren<MeshFilter>(true);
            for (var i = 0; i < filters.Length; i++)
            {
                if (IsFlatMesh(filters[i] != null ? filters[i].sharedMesh : null))
                    return true;
            }
            return false;
        }

        private static bool IsFlatMesh(Mesh mesh)
        {
            if (mesh == null) return false;
            var n = mesh.name;
            return n.IndexOf("Plane", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   n.IndexOf("Quad", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static float MaxWorldSize(Transform t)
        {
            if (t == null) return 0f;
            var rends = t.GetComponentsInChildren<Renderer>(true);
            var any = false;
            var bounds = new Bounds(t.position, Vector3.zero);
            for (var i = 0; i < rends.Length; i++)
            {
                var r = rends[i];
                if (r == null || !r.enabled) continue;
                if (!any)
                {
                    bounds = r.bounds;
                    any = true;
                }
                else
                {
                    bounds.Encapsulate(r.bounds);
                }
            }

            if (!any) return 0f;
            return Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
        }

        private Transform ResolveHandAttach()
        {
            if (heldAttachPoint != null)
                return heldAttachPoint;

            if (animator == null)
                animator = GetComponentInChildren<Animator>();
            var root = animator != null ? animator.transform : transform;
            if (animator != null && animator.isHuman)
            {
                var human = animator.GetBoneTransform(HumanBodyBones.RightHand);
                if (human != null)
                    return human;
            }

            var named = FindRightHand(root);
            if (named != null)
                return named;
            var raised = FindRaisedRightLimb(root);
            return raised != null ? raised : transform;
        }

        private static Transform FindRaisedRightLimb(Transform root)
        {
            if (root == null) return null;
            Transform best = null;
            var bestScore = float.MinValue;
            var all = root.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < all.Length; i++)
            {
                var t = all[i];
                if (t == null || t == root) continue;
                var n = t.name;
                if (n.IndexOf("Left", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;
                if (n.StartsWith("Held", System.StringComparison.OrdinalIgnoreCase))
                    continue;
                if (n.IndexOf("Toe", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    n.IndexOf("Foot", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    n.IndexOf("Heel", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;

                var local = root.InverseTransformPoint(t.position);
                if (local.y < 0.35f) continue;
                var score = local.y * 1.6f + local.x * 1.1f + local.z * 0.25f;
                if (n.IndexOf("Hand", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    n.IndexOf("Wrist", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    n.IndexOf("Arm", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    score += 4f;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = t;
                }
            }

            return best;
        }

        private static Transform FindRightHand(Transform root)
        {
            if (root == null) return null;
            Transform best = null;
            var bestScore = int.MinValue;
            var all = root.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < all.Length; i++)
            {
                var t = all[i];
                if (t == null) continue;
                var n = t.name;
                if (n.IndexOf("Left", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;
                if (n.IndexOf("Handle", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;

                var isHand = n.IndexOf("Hand", System.StringComparison.OrdinalIgnoreCase) >= 0;
                var isWrist = n.IndexOf("Wrist", System.StringComparison.OrdinalIgnoreCase) >= 0;
                var isPalm = n.IndexOf("Palm", System.StringComparison.OrdinalIgnoreCase) >= 0;
                if (!isHand && !isWrist && !isPalm) continue;

                var isRight =
                    n.IndexOf("Right", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    n.EndsWith("_R", System.StringComparison.OrdinalIgnoreCase) ||
                    n.EndsWith(".R", System.StringComparison.OrdinalIgnoreCase) ||
                    n.EndsWith(" R", System.StringComparison.OrdinalIgnoreCase) ||
                    n.IndexOf("_R_", System.StringComparison.OrdinalIgnoreCase) >= 0;
                if (!isRight) continue;

                var score = (isHand ? 8 : 0) + (isWrist ? 5 : 0) + (isPalm ? 4 : 0) + Depth(t);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = t;
                }
            }

            return best;
        }

        private static int Depth(Transform t)
        {
            var d = 0;
            while (t != null)
            {
                d++;
                t = t.parent;
            }
            return d;
        }

        private static GameObject CreateRodPlaceholder(Transform parent)
        {
            var root = new GameObject("HeldRod_Placeholder");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(0.25f, 0.9f, 0.35f);
            root.transform.localRotation = Quaternion.Euler(15f, 0f, -20f);

            var pole = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            pole.name = "Pole";
            pole.transform.SetParent(root.transform, false);
            pole.transform.localScale = new Vector3(0.06f, 0.85f, 0.06f);
            pole.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            Object.Destroy(pole.GetComponent<Collider>());
            Tint(pole, new Color(0.45f, 0.28f, 0.14f, 1f));

            return root;
        }

        private static GameObject CreateNetPlaceholder(Transform parent)
        {
            var root = new GameObject("HeldNet_Placeholder");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(0.3f, 0.85f, 0.3f);
            root.transform.localRotation = Quaternion.Euler(60f, 0f, 0f);

            var hoop = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            hoop.name = "Hoop";
            hoop.transform.SetParent(root.transform, false);
            hoop.transform.localScale = new Vector3(0.55f, 0.03f, 0.55f);
            Object.Destroy(hoop.GetComponent<Collider>());
            Tint(hoop, new Color(0.2f, 0.35f, 0.4f, 1f));

            var bag = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bag.name = "Bag";
            bag.transform.SetParent(root.transform, false);
            bag.transform.localScale = new Vector3(0.45f, 0.35f, 0.45f);
            bag.transform.localPosition = new Vector3(0f, -0.15f, 0f);
            Object.Destroy(bag.GetComponent<Collider>());
            Tint(bag, new Color(0.3f, 0.55f, 0.6f, 0.7f));

            var handle = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            handle.name = "Handle";
            handle.transform.SetParent(root.transform, false);
            handle.transform.localScale = new Vector3(0.05f, 0.35f, 0.05f);
            handle.transform.localPosition = new Vector3(0f, 0.35f, 0f);
            Object.Destroy(handle.GetComponent<Collider>());
            Tint(handle, new Color(0.4f, 0.25f, 0.12f, 1f));

            return root;
        }

        private static GameObject CreateLanternPlaceholder(Transform parent)
        {
            var root = new GameObject("HeldLantern_Placeholder");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(0.28f, 0.85f, 0.28f);
            root.transform.localRotation = Quaternion.identity;

            var body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localScale = new Vector3(0.22f, 0.18f, 0.22f);
            Object.Destroy(body.GetComponent<Collider>());
            Tint(body, new Color(0.55f, 0.35f, 0.15f, 1f));

            var glass = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            glass.name = "Glass";
            glass.transform.SetParent(root.transform, false);
            glass.transform.localScale = new Vector3(0.2f, 0.22f, 0.2f);
            glass.transform.localPosition = new Vector3(0f, 0.12f, 0f);
            Object.Destroy(glass.GetComponent<Collider>());
            Tint(glass, new Color(1f, 0.85f, 0.4f, 0.85f));

            var handle = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            handle.name = "Handle";
            handle.transform.SetParent(root.transform, false);
            handle.transform.localScale = new Vector3(0.04f, 0.12f, 0.04f);
            handle.transform.localPosition = new Vector3(0f, 0.32f, 0f);
            Object.Destroy(handle.GetComponent<Collider>());
            Tint(handle, new Color(0.35f, 0.35f, 0.38f, 1f));

            root.AddComponent<HeldLantern>();
            return root;
        }

        private static void Tint(GameObject go, Color color)
        {
            var rend = go.GetComponent<MeshRenderer>();
            if (rend == null) return;
            rend.sharedMaterial = Bayou.Rendering.BayouShaderUtil.CreateUnlitColor(color);
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.enabled = true;
        }

        private bool WasSwitch()
        {
            if (switchToolAction?.action != null && switchToolAction.action.WasPressedThisFrame())
                return true;

            var kb = Keyboard.current;
            return kb != null && kb.tabKey.wasPressedThisFrame;
        }

        private static bool WasSelect(InputActionReference actionRef, params Key[] keys)
        {
            if (actionRef?.action != null && actionRef.action.WasPressedThisFrame())
                return true;

            var kb = Keyboard.current;
            if (kb == null) return false;
            foreach (var key in keys)
            {
                if (kb[key].wasPressedThisFrame)
                    return true;
            }

            return false;
        }
    }
}
