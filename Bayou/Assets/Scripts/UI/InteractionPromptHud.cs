using Bayou.Inventory.Shop;
using Bayou.Save;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Bayou.UI
{
    /// <summary>
    /// Bottom-center contextual control hint: [E] Talk, [LMB] Scoop, etc.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InteractionPromptHud : MonoBehaviour
    {
        [SerializeField] private Canvas canvas;
        [SerializeField] private RectTransform root;
        [SerializeField] private Image buttonBadge;
        [SerializeField] private TextMeshProUGUI buttonLabel;
        [SerializeField] private TextMeshProUGUI actionLabel;
        [SerializeField] private bool buildUiIfMissing = true;

        private string _lastLine;

        public static InteractionPromptHud Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
            if (buildUiIfMissing && (canvas == null || root == null))
                BuildUi();
            SetVisible(false);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void LateUpdate()
        {
            if (ShouldHideForMenus())
            {
                SetVisible(false);
                return;
            }

            if (!InteractionPromptBroker.TryGetBest(out var prompt))
            {
                SetVisible(false);
                return;
            }

            Apply(prompt);
            SetVisible(true);
        }

        private void Apply(InteractionPrompt prompt)
        {
            var line = prompt.DisplayLine;
            if (line == _lastLine) return;
            _lastLine = line;

            if (buttonLabel != null)
                buttonLabel.text = string.IsNullOrWhiteSpace(prompt.Button) ? "" : prompt.Button;

            if (buttonBadge != null)
                buttonBadge.gameObject.SetActive(!string.IsNullOrWhiteSpace(prompt.Button));

            if (actionLabel != null)
                actionLabel.text = prompt.Action ?? "";
        }

        private void SetVisible(bool visible)
        {
            if (root != null && root.gameObject.activeSelf != visible)
                root.gameObject.SetActive(visible);
            if (!visible)
                _lastLine = null;
        }

        private static bool ShouldHideForMenus()
        {
            if (AudioSettings.IsOpen) return true;
            if (ShopUIController.ActiveShop != null && ShopUIController.ActiveShop.IsOpen) return true;
            if (BonfireUIController.Active != null && BonfireUIController.Active.IsOpen) return true;
            var dialogue = DialogueManager.GetInstance();
            // During dialogue, the dialogue UI owns Continue — don't stack a world prompt.
            if (dialogue != null && dialogue.dialogueIsPlaying) return true;
            return false;
        }

        private void BuildUi()
        {
            var canvasGo = new GameObject("InteractionPromptCanvas");
            canvasGo.transform.SetParent(transform, false);
            canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>().enabled = false;

            var rootGo = new GameObject("Prompt", typeof(RectTransform));
            rootGo.transform.SetParent(canvasGo.transform, false);
            root = rootGo.GetComponent<RectTransform>();
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0f);
            root.pivot = new Vector2(0.5f, 0f);
            root.anchoredPosition = new Vector2(0f, 96f);
            root.sizeDelta = new Vector2(420f, 56f);

            var bg = rootGo.AddComponent<Image>();
            bg.color = new Color(0.04f, 0.06f, 0.05f, 0.62f);
            bg.raycastTarget = false;

            // Button badge
            var badgeGo = new GameObject("ButtonBadge", typeof(RectTransform));
            badgeGo.transform.SetParent(root, false);
            var badgeRt = badgeGo.GetComponent<RectTransform>();
            badgeRt.anchorMin = badgeRt.anchorMax = new Vector2(0f, 0.5f);
            badgeRt.pivot = new Vector2(0f, 0.5f);
            badgeRt.anchoredPosition = new Vector2(14f, 0f);
            badgeRt.sizeDelta = new Vector2(48f, 36f);
            buttonBadge = badgeGo.AddComponent<Image>();
            buttonBadge.color = new Color(0.86f, 0.78f, 0.42f, 0.95f);
            buttonBadge.raycastTarget = false;

            buttonLabel = CreateTmp("ButtonText", badgeGo.transform, "E", 20f, FontStyles.Bold,
                TextAlignmentOptions.Center, Color.black);
            Stretch(buttonLabel.rectTransform, 2f, 2f, 2f, 2f);

            actionLabel = CreateTmp("ActionText", root, "Interact", 22f, FontStyles.Normal,
                TextAlignmentOptions.Left, new Color(0.94f, 0.95f, 0.9f, 0.98f));
            var actionRt = actionLabel.rectTransform;
            actionRt.anchorMin = new Vector2(0f, 0f);
            actionRt.anchorMax = new Vector2(1f, 1f);
            actionRt.offsetMin = new Vector2(74f, 8f);
            actionRt.offsetMax = new Vector2(-16f, -8f);
        }

        private static TextMeshProUGUI CreateTmp(
            string name,
            Transform parent,
            string text,
            float size,
            FontStyles style,
            TextAlignmentOptions align,
            Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.alignment = align;
            tmp.color = color;
            tmp.raycastTarget = false;
            if (TMP_Settings.defaultFontAsset != null)
                tmp.font = TMP_Settings.defaultFontAsset;
            return tmp;
        }

        private static void Stretch(RectTransform rt, float l, float t, float r, float b)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(l, b);
            rt.offsetMax = new Vector2(-r, -t);
        }

        public static InteractionPromptHud EnsureInScene()
        {
            if (Instance != null) return Instance;
            var existing = Object.FindFirstObjectByType<InteractionPromptHud>(FindObjectsInactive.Include);
            if (existing != null) return existing;
            var go = new GameObject("InteractionPromptHud");
            return go.AddComponent<InteractionPromptHud>();
        }
    }
}
