#if !ENABLE_INPUT_SYSTEM
#error BayouFishingEquipment requires the New Input System (ENABLE_INPUT_SYSTEM).
#endif

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace Bayou.Fishing
{
    public enum BayouFishingTool
    {
        Rod,
        Net
    }

    /// <summary>
    /// Switches between two distinct tools:
    /// <list type="bullet">
    /// <item><see cref="BayouFishingTool.Rod"/> — long cast / attract / reel (<see cref="FishingNetCaster"/>)</item>
    /// <item><see cref="BayouFishingTool.Net"/> — short-range scoop net (<see cref="HandNetAreaController"/>)</item>
    /// </list>
    /// Also toggles held rod/net visuals (assign your meshes, or placeholders are created).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BayouFishingEquipment : MonoBehaviour
    {
        [Header("Tools")]
        [FormerlySerializedAs("throwingCaster")]
        [SerializeField] private FishingNetCaster rodCaster;
        [SerializeField] private HandNetAreaController handNet;

        [Header("Held visuals (two different objects)")]
        [Tooltip("Mesh/object held when Rod is equipped. Leave empty to spawn a placeholder.")]
        [SerializeField] private GameObject heldRod;

        [Tooltip("Mesh/object held when Net is equipped. Leave empty to spawn a placeholder.")]
        [SerializeField] private GameObject heldNet;

        [SerializeField] private Transform heldAttachPoint;
        [SerializeField] private bool createPlaceholdersIfMissing = true;

        [Header("Input")]
        [Tooltip("Cycle Rod ↔ Net. Default fallback: Tab.")]
        [SerializeField] private InputActionReference switchToolAction;

        [SerializeField] private InputActionReference selectRodAction;
        [SerializeField] private InputActionReference selectNetAction;

        [SerializeField] private BayouFishingTool startingTool = BayouFishingTool.Rod;

        public BayouFishingTool CurrentTool { get; private set; } = BayouFishingTool.Rod;

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
            selectRodAction?.action?.Enable();
            selectNetAction?.action?.Enable();
            ApplyTool(startingTool);
        }

        private void OnDisable()
        {
            switchToolAction?.action?.Disable();
            selectRodAction?.action?.Disable();
            selectNetAction?.action?.Disable();
        }

        private void Update()
        {
            if (WasSelectRod())
            {
                ApplyTool(BayouFishingTool.Rod);
                return;
            }

            if (WasSelectNet())
            {
                ApplyTool(BayouFishingTool.Net);
                return;
            }

            if (WasSwitch())
            {
                ApplyTool(CurrentTool == BayouFishingTool.Rod
                    ? BayouFishingTool.Net
                    : BayouFishingTool.Rod);
            }
        }

        public void ApplyTool(BayouFishingTool tool)
        {
            // Don't switch mid-cast / while a net is planted.
            if (rodCaster != null && (rodCaster.Phase != FishingCastPhase.Idle || rodCaster.HasActiveNet))
            {
                if (tool != BayouFishingTool.Rod && CurrentTool == BayouFishingTool.Rod)
                    return;
            }

            CurrentTool = tool;

            if (rodCaster != null)
                rodCaster.enabled = tool == BayouFishingTool.Rod;

            if (handNet != null)
                handNet.enabled = tool == BayouFishingTool.Net;

            if (heldRod != null)
                heldRod.SetActive(tool == BayouFishingTool.Rod);

            if (heldNet != null)
                heldNet.SetActive(tool == BayouFishingTool.Net);
        }

        private void EnsureHeldVisuals()
        {
            var attach = heldAttachPoint != null ? heldAttachPoint : transform;

            if (heldRod == null && createPlaceholdersIfMissing)
                heldRod = CreateRodPlaceholder(attach);

            if (heldNet == null && createPlaceholdersIfMissing)
                heldNet = CreateNetPlaceholder(attach);
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
            Destroy(pole.GetComponent<Collider>());
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
            Destroy(hoop.GetComponent<Collider>());
            Tint(hoop, new Color(0.2f, 0.35f, 0.4f, 1f));

            var bag = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bag.name = "Bag";
            bag.transform.SetParent(root.transform, false);
            bag.transform.localScale = new Vector3(0.45f, 0.35f, 0.45f);
            bag.transform.localPosition = new Vector3(0f, -0.15f, 0f);
            Destroy(bag.GetComponent<Collider>());
            Tint(bag, new Color(0.3f, 0.55f, 0.6f, 0.7f));

            var handle = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            handle.name = "Handle";
            handle.transform.SetParent(root.transform, false);
            handle.transform.localScale = new Vector3(0.05f, 0.35f, 0.05f);
            handle.transform.localPosition = new Vector3(0f, 0.35f, 0f);
            Destroy(handle.GetComponent<Collider>());
            Tint(handle, new Color(0.4f, 0.25f, 0.12f, 1f));

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

        private bool WasSelectRod()
        {
            if (selectRodAction?.action != null && selectRodAction.action.WasPressedThisFrame())
                return true;

            var kb = Keyboard.current;
            return kb != null && kb.digit1Key.wasPressedThisFrame;
        }

        private bool WasSelectNet()
        {
            if (selectNetAction?.action != null && selectNetAction.action.WasPressedThisFrame())
                return true;

            var kb = Keyboard.current;
            return kb != null && kb.digit2Key.wasPressedThisFrame;
        }
    }
}
