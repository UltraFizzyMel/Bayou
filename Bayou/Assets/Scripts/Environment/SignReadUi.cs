using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Bayou.Environment
{
    /// <summary>Screen panel for a sign or note. Press E again, or walk away, to close.</summary>
    public sealed class SignReadUi : MonoBehaviour
    {
        public static SignReadUi Instance { get; private set; }

        private Canvas _canvas;
        private GameObject _panel;
        private TextMeshProUGUI _title;
        private TextMeshProUGUI _body;
        private Object _owner;
        private int _openedFrame = -1;
        private string _guiTitle = "";
        private string _guiBody = "";

        public static bool IsOpen => Instance != null && Instance._panel != null && Instance._panel.activeSelf;
        public static Object Owner => Instance != null ? Instance._owner : null;

        public static void Show(Object owner, string title, string body)
        {
            var ui = Instance;
            if (ui == null)
            {
                var go = new GameObject("SignReadUi");
                ui = go.AddComponent<SignReadUi>();
            }

            ui.Present(owner, title, body);
        }

        public static void Hide(Object owner)
        {
            if (Instance == null) return;
            if (Time.frameCount == Instance._openedFrame) return;
            if (owner != null && Instance._owner != owner) return;
            Instance.Close();
        }

        private void Awake()
        {
            Instance = this;
            Build();
            Close();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void Present(Object owner, string title, string body)
        {
            Build();
            _openedFrame = Time.frameCount;
            _owner = owner;
            _guiTitle = string.IsNullOrWhiteSpace(title) ? "Sign" : title;
            _guiBody = string.IsNullOrWhiteSpace(body) ? "..." : body;
            if (_title != null)
                _title.text = _guiTitle;
            if (_body != null)
                _body.text = _guiBody;
            if (_panel != null)
                _panel.SetActive(true);
        }

        private void OnGUI()
        {
            if (!IsOpen) return;

            const float width = 680f;
            const float height = 300f;
            var rect = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            var previous = GUI.color;
            GUI.color = new Color(0.07f, 0.06f, 0.04f, 0.94f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = new Color(0.96f, 0.93f, 0.84f, 1f);
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                wordWrap = true,
                alignment = TextAnchor.UpperLeft
            };
            GUI.Label(new Rect(rect.x + 28f, rect.y + 24f, width - 56f, height - 48f),
                _guiTitle + "\n\n" + _guiBody + "\n\nPress E to close", style);
            GUI.color = previous;
        }

        private void Close()
        {
            _owner = null;
            if (_panel != null)
                _panel.SetActive(false);
        }

        private void Build()
        {
            if (_canvas != null && _panel != null) return;

            var canvasGo = new GameObject("SignCanvas");
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 400;
            _canvas.overrideSorting = true;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            _panel = new GameObject("SignPanel", typeof(RectTransform));
            _panel.transform.SetParent(canvasGo.transform, false);
            var rt = _panel.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(640f, 320f);
            var img = _panel.AddComponent<Image>();
            img.sprite = WhiteSprite();
            img.color = new Color(0.08f, 0.07f, 0.05f, 0.92f);
            img.raycastTarget = false;

            _title = CreateText("Title", _panel.transform, 28f, FontStyles.Bold, TextAlignmentOptions.TopLeft);
            var titleRt = _title.rectTransform;
            titleRt.anchorMin = new Vector2(0f, 1f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.anchoredPosition = new Vector2(0f, -18f);
            titleRt.sizeDelta = new Vector2(-48f, 42f);

            _body = CreateText("Body", _panel.transform, 22f, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            var bodyRt = _body.rectTransform;
            bodyRt.anchorMin = Vector2.zero;
            bodyRt.anchorMax = Vector2.one;
            bodyRt.offsetMin = new Vector2(28f, 56f);
            bodyRt.offsetMax = new Vector2(-28f, -72f);

            var hint = CreateText("Hint", _panel.transform, 16f, FontStyles.Italic, TextAlignmentOptions.BottomRight);
            hint.text = "E  close";
            hint.color = new Color(0.78f, 0.74f, 0.62f, 0.9f);
            var hintRt = hint.rectTransform;
            hintRt.anchorMin = new Vector2(1f, 0f);
            hintRt.anchorMax = new Vector2(1f, 0f);
            hintRt.pivot = new Vector2(1f, 0f);
            hintRt.anchoredPosition = new Vector2(-20f, 16f);
            hintRt.sizeDelta = new Vector2(160f, 28f);
        }

        private static TextMeshProUGUI CreateText(
            string name,
            Transform parent,
            float size,
            FontStyles style,
            TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.alignment = align;
            tmp.color = new Color(0.94f, 0.91f, 0.82f, 1f);
            tmp.enableWordWrapping = true;
            tmp.raycastTarget = false;
            tmp.overflowMode = TextOverflowModes.Overflow;
            var font = TMP_Settings.defaultFontAsset;
            if (font == null)
                font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (font != null)
                tmp.font = font;
            return tmp;
        }

        private static Sprite WhiteSprite()
        {
            var tex = Texture2D.whiteTexture;
            return Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 4f);
        }
    }
}
