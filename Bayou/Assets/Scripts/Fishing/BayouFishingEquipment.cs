#if !ENABLE_INPUT_SYSTEM
#error BayouFishingEquipment requires the New Input System (ENABLE_INPUT_SYSTEM).
#endif

using Bayou.Inventory;
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
        [SerializeField] private bool createPlaceholdersIfMissing = true;

        [Header("Input")]
        [SerializeField] private InputActionReference switchToolAction;
        [SerializeField] private InputActionReference selectNoneAction;
        [SerializeField] private InputActionReference selectRodAction;
        [SerializeField] private InputActionReference selectNetAction;
        [SerializeField] private InputActionReference selectLanternAction;

        [SerializeField] private BayouHeldItem startingItem = BayouHeldItem.Net;
        [Tooltip("Rod can only be held after buying Item_FishingRod from Caliste.")]
        [SerializeField] private bool requireRodItem = true;
        [SerializeField] private string fishingRodItemId = "Item_FishingRod";
        [Tooltip("Lantern can only be held after picking up Item_Lantern.")]
        [SerializeField] private bool requireLanternItem = true;
        [SerializeField] private string lanternItemId = "Item_Lantern";
        [SerializeField] private Animator animator;

        private static readonly BayouHeldItem[] CycleOrder =
        {
            BayouHeldItem.None,
            BayouHeldItem.Rod,
            BayouHeldItem.Net,
            BayouHeldItem.Lantern
        };

        private HeldLantern _lantern;

        public BayouHeldItem CurrentItem { get; private set; } = BayouHeldItem.None;

        /// <summary>Alias for older call sites / HUD.</summary>
        public BayouFishingTool CurrentTool => (BayouFishingTool)CurrentItem;

        public bool IsHolding(BayouHeldItem item) => CurrentItem == item;

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

            EnsureHeldVisuals();
        }

        private void OnEnable()
        {
            switchToolAction?.action?.Enable();
            selectNoneAction?.action?.Enable();
            selectRodAction?.action?.Enable();
            selectNetAction?.action?.Enable();
            selectLanternAction?.action?.Enable();
            ApplyItem(startingItem);
        }

        private void OnDisable()
        {
            switchToolAction?.action?.Disable();
            selectNoneAction?.action?.Disable();
            selectRodAction?.action?.Disable();
            selectNetAction?.action?.Disable();
            selectLanternAction?.action?.Disable();
        }

        private void Update()
        {
            if (WasSelect(selectNoneAction, Key.Digit0, Key.Backquote))
            {
                ApplyItem(BayouHeldItem.None);
                animator.SetBool("isHoldingRod", false);
                animator.SetBool("isHoldingLantern", false);
                return;
            }

            if (WasSelect(selectRodAction, Key.Digit1))
            {
                if (!CanHold(BayouHeldItem.Rod))
                {
                    Debug.Log("[Equipment] Buy a Fishing Rod from Caliste first.");
                    return;
                }

                animator.SetBool("isHoldingRod", true);
                animator.SetBool("isHoldingLantern", false);
                ApplyItem(BayouHeldItem.Rod);
                return;
            }

            if (WasSelect(selectNetAction, Key.Digit2))
            {
                animator.SetBool("isHoldingRod", true);
                animator.SetBool("isHoldingLantern", false);
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

                animator.SetBool("isHoldingRod", false);
                animator.SetBool("isHoldingLantern", true);
                ApplyItem(BayouHeldItem.Lantern);
                return;
            }

            if (WasSwitch())
                CycleNext();
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
                item = BayouHeldItem.Net;

            // Don't leave the rod mid-cast / while a line is out.
            if (CurrentItem == BayouHeldItem.Rod && item != BayouHeldItem.Rod &&
                rodCaster != null &&
                (rodCaster.Phase != FishingCastPhase.Idle || rodCaster.HasActiveNet))
            {
                return;
            }

            CurrentItem = item;

            if (rodCaster != null)
                rodCaster.enabled = item == BayouHeldItem.Rod;

            if (handNet != null)
                handNet.enabled = item == BayouHeldItem.Net;

            if (heldRod != null)
                heldRod.SetActive(item == BayouHeldItem.Rod);

            if (heldNet != null)
                heldNet.SetActive(item == BayouHeldItem.Net);

            if (heldLantern != null)
                heldLantern.SetActive(item == BayouHeldItem.Lantern);

            if (_lantern == null && heldLantern != null)
                _lantern = heldLantern.GetComponent<HeldLantern>() ??
                           heldLantern.GetComponentInChildren<HeldLantern>(true);

            _lantern?.SetLit(item == BayouHeldItem.Lantern);
        }

        private void EnsureHeldVisuals()
        {
            var attach = heldAttachPoint != null ? heldAttachPoint : transform;

            if (heldRod == null && createPlaceholdersIfMissing)
                heldRod = CreateRodPlaceholder(attach);

            if (heldNet == null && createPlaceholdersIfMissing)
                heldNet = CreateNetPlaceholder(attach);

            if (heldLantern == null && createPlaceholdersIfMissing)
                heldLantern = CreateLanternPlaceholder(attach);

            if (heldLantern != null)
            {
                _lantern = heldLantern.GetComponent<HeldLantern>();
                if (_lantern == null)
                    _lantern = heldLantern.AddComponent<HeldLantern>();
            }
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
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            var mat = new Material(shader);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            else if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            rend.sharedMaterial = mat;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
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
