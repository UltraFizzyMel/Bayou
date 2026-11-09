using Bayou.Inventory;
using Bayou.Inventory.Shop;
using Bayou.Save;
using UnityEngine;
using UnityEngine.UI;

namespace Bayou.UI
{
    /// <summary>
    /// Esc opens this. Resume, graphics, audio, or quit.
    /// </summary>
    [DefaultExecutionOrder(40)]
    public sealed class PauseMenu : MonoBehaviour
    {
        const string PrefQuality = "Bayou.Gfx.Quality";
        const string PrefFull = "Bayou.Gfx.Fullscreen";
        const string PrefVSync = "Bayou.Gfx.VSync";
        const string PrefWidth = "Bayou.Gfx.Width";
        const string PrefHeight = "Bayou.Gfx.Height";

        public static bool IsOpen { get; private set; }

        private static PauseMenu _instance;
        private static bool _graphicsApplied;

        private GameObject _root;
        private GameObject _mainPage;
        private GameObject _settingsPage;
        private Text _qualityLabel;
        private Text _fullLabel;
        private Text _vsyncLabel;
        private Text _resolutionLabel;
        private Slider _master;
        private Slider _music;
        private Slider _sfx;
        private bool _suppress;

        public static void Ensure()
        {
            ApplySavedGraphics();
            if (_instance != null) return;
            var go = new GameObject(nameof(PauseMenu));
            _instance = go.AddComponent<PauseMenu>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            Build();
            SetOpen(false);
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
            if (IsOpen)
            {
                IsOpen = false;
                GameplayPause.SyncFromUiState();
            }
        }

        private void Update()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null || !kb.escapeKey.wasPressedThisFrame) return;
            if (EscapeGate.Consumed) return;
            if (MenuOwnsEscape()) return;

            if (!IsOpen)
            {
                ShowMain();
                return;
            }

            if (_settingsPage != null && _settingsPage.activeSelf)
            {
                ShowMain();
                return;
            }

            SetOpen(false);
        }

        private static bool MenuOwnsEscape()
        {
            var dialogue = DialogueManager.GetInstance();
            if (dialogue != null && dialogue.dialogueIsPlaying) return true;
            if (InventoryDisplayUI.Active != null && InventoryDisplayUI.Active.IsOpen) return true;
            if (ShopUIController.ActiveShop != null && ShopUIController.ActiveShop.IsOpen) return true;
            if (QuestJournalHud.IsOpen) return true;
            if (BonfireUIController.Active != null && BonfireUIController.Active.IsOpen) return true;
            if (CaughtFishPresenter.IsBusy) return true;
            if (AudioSettings.IsOpen) return true;
            return false;
        }

        private void ShowMain()
        {
            SetOpen(true);
            if (_mainPage != null) _mainPage.SetActive(true);
            if (_settingsPage != null) _settingsPage.SetActive(false);
        }

        private void ShowSettings()
        {
            SetOpen(true);
            if (_mainPage != null) _mainPage.SetActive(false);
            if (_settingsPage != null) _settingsPage.SetActive(true);
            RefreshSettingsLabels();
        }

        private void SetOpen(bool open)
        {
            IsOpen = open;
            if (_root != null) _root.SetActive(open);
            GameplayPause.SyncFromUiState();
        }

        private void Build()
        {
            var canvasGo = new GameObject("PauseCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 420;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            _root = canvasGo;

            var dim = CreateRect("Dim", canvasGo.transform);
            Stretch(dim);
            var dimImage = dim.gameObject.AddComponent<Image>();
            dimImage.color = new Color(0.04f, 0.05f, 0.04f, 0.72f);

            var panel = CreateRect("Panel", canvasGo.transform);
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(520f, 720f);
            var panelImage = panel.gameObject.AddComponent<Image>();
            panelImage.color = new Color(0.11f, 0.1f, 0.08f, 0.96f);

            var title = CreateText("Title", panel, "Paused", 36, FontStyle.Bold);
            title.rectTransform.anchoredPosition = new Vector2(0f, 300f);
            title.color = new Color(0.96f, 0.91f, 0.78f, 1f);

            _mainPage = new GameObject("Main", typeof(RectTransform));
            _mainPage.transform.SetParent(panel, false);
            Stretch(_mainPage.GetComponent<RectTransform>());
            AddButton(_mainPage.transform, "Resume", new Vector2(0f, 60f), () => SetOpen(false));
            AddButton(_mainPage.transform, "Settings", new Vector2(0f, -20f), ShowSettings);
            AddButton(_mainPage.transform, "Quit", new Vector2(0f, -100f), QuitGame);

            _settingsPage = new GameObject("Settings", typeof(RectTransform));
            _settingsPage.transform.SetParent(panel, false);
            Stretch(_settingsPage.GetComponent<RectTransform>());
            _settingsPage.SetActive(false);

            _qualityLabel = AddButton(_settingsPage.transform, "Quality", new Vector2(0f, 200f), CycleQuality);
            _resolutionLabel = AddButton(_settingsPage.transform, "Resolution", new Vector2(0f, 130f), CycleResolution);
            _fullLabel = AddButton(_settingsPage.transform, "Fullscreen", new Vector2(0f, 60f), ToggleFullscreen);
            _vsyncLabel = AddButton(_settingsPage.transform, "VSync", new Vector2(0f, -10f), ToggleVSync);
            _master = AddSlider(_settingsPage.transform, "Master", new Vector2(0f, -90f), AudioSettings.PrefMaster, "MasterVolume");
            _music = AddSlider(_settingsPage.transform, "Music", new Vector2(0f, -150f), AudioSettings.PrefMusic, "MusicVolume");
            _sfx = AddSlider(_settingsPage.transform, "Effects", new Vector2(0f, -210f), AudioSettings.PrefSfx, "SFXVolume");
            AddButton(_settingsPage.transform, "Back", new Vector2(0f, -290f), ShowMain);
        }

        private Text AddButton(Transform parent, string label, Vector2 pos, UnityEngine.Events.UnityAction onClick)
        {
            var rt = CreateRect(label, parent);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(360f, 52f);
            var image = rt.gameObject.AddComponent<Image>();
            image.color = new Color(0.22f, 0.2f, 0.14f, 1f);
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);
            var text = CreateText("Label", rt, label, 22, FontStyle.Bold);
            text.color = new Color(0.96f, 0.91f, 0.78f, 1f);
            Stretch(text.rectTransform);
            return text;
        }

        private Slider AddSlider(Transform parent, string label, Vector2 pos, string prefKey, string mixerParam)
        {
            var caption = CreateText(label + "Caption", parent, label, 18, FontStyle.Normal);
            caption.rectTransform.anchorMin = caption.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            caption.rectTransform.anchoredPosition = pos + new Vector2(-150f, 0f);
            caption.rectTransform.sizeDelta = new Vector2(120f, 32f);
            caption.alignment = TextAnchor.MiddleRight;
            caption.color = new Color(0.86f, 0.8f, 0.68f, 1f);

            var rt = CreateRect(label + "Slider", parent);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos + new Vector2(70f, 0f);
            rt.sizeDelta = new Vector2(220f, 28f);
            var bg = rt.gameObject.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.08f, 0.07f, 1f);

            var fillArea = CreateRect("Fill", rt);
            Stretch(fillArea);
            var fill = fillArea.gameObject.AddComponent<Image>();
            fill.color = new Color(0.55f, 0.46f, 0.24f, 1f);

            var slider = rt.gameObject.AddComponent<Slider>();
            slider.fillRect = fillArea;
            slider.targetGraphic = bg;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            var db = AudioSettings.GetBusDb(prefKey, 0f);
            _suppress = true;
            slider.SetValueWithoutNotify(Mathf.InverseLerp(-80f, 0f, db));
            _suppress = false;
            slider.onValueChanged.AddListener(value =>
            {
                if (_suppress) return;
                AudioSettings.SetBusDb(prefKey, mixerParam, Mathf.Lerp(-80f, 0f, value));
            });
            return slider;
        }

        private void RefreshSettingsLabels()
        {
            var names = QualitySettings.names;
            var index = Mathf.Clamp(QualitySettings.GetQualityLevel(), 0, Mathf.Max(0, names.Length - 1));
            if (_qualityLabel != null)
                _qualityLabel.text = names.Length == 0 ? "Quality" : "Quality: " + names[index];
            if (_fullLabel != null)
                _fullLabel.text = Screen.fullScreen ? "Fullscreen: On" : "Fullscreen: Off";
            if (_vsyncLabel != null)
                _vsyncLabel.text = QualitySettings.vSyncCount > 0 ? "VSync: On" : "VSync: Off";
            if (_resolutionLabel != null)
                _resolutionLabel.text = $"Resolution: {Screen.width} x {Screen.height}";
        }

        private void CycleQuality()
        {
            var names = QualitySettings.names;
            if (names == null || names.Length == 0) return;
            var next = (QualitySettings.GetQualityLevel() + 1) % names.Length;
            QualitySettings.SetQualityLevel(next, true);
            PlayerPrefs.SetInt(PrefQuality, next);
            PlayerPrefs.Save();
            RefreshSettingsLabels();
        }

        private void CycleResolution()
        {
            var options = Screen.resolutions;
            if (options == null || options.Length == 0) return;
            var current = 0;
            for (var i = 0; i < options.Length; i++)
            {
                if (options[i].width == Screen.width && options[i].height == Screen.height)
                {
                    current = i;
                    break;
                }
            }

            var next = options[(current + 1) % options.Length];
            Screen.SetResolution(next.width, next.height, Screen.fullScreen);
            PlayerPrefs.SetInt(PrefWidth, next.width);
            PlayerPrefs.SetInt(PrefHeight, next.height);
            RefreshSettingsLabels();
        }

        private void ToggleFullscreen()
        {
            Screen.fullScreen = !Screen.fullScreen;
            PlayerPrefs.SetInt(PrefFull, Screen.fullScreen ? 1 : 0);
            PlayerPrefs.Save();
            RefreshSettingsLabels();
        }

        private void ToggleVSync()
        {
            var on = QualitySettings.vSyncCount <= 0;
            QualitySettings.vSyncCount = on ? 1 : 0;
            PlayerPrefs.SetInt(PrefVSync, on ? 1 : 0);
            PlayerPrefs.Save();
            RefreshSettingsLabels();
        }

        private static void ApplySavedGraphics()
        {
            if (_graphicsApplied) return;
            _graphicsApplied = true;

            if (PlayerPrefs.HasKey(PrefQuality))
            {
                var level = Mathf.Clamp(PlayerPrefs.GetInt(PrefQuality), 0, Mathf.Max(0, QualitySettings.names.Length - 1));
                QualitySettings.SetQualityLevel(level, true);
            }

            if (PlayerPrefs.HasKey(PrefVSync))
                QualitySettings.vSyncCount = PlayerPrefs.GetInt(PrefVSync) > 0 ? 1 : 0;

            if (PlayerPrefs.HasKey(PrefFull))
                Screen.fullScreen = PlayerPrefs.GetInt(PrefFull) > 0;

            if (PlayerPrefs.HasKey(PrefWidth) && PlayerPrefs.HasKey(PrefHeight))
            {
                Screen.SetResolution(
                    PlayerPrefs.GetInt(PrefWidth),
                    PlayerPrefs.GetInt(PrefHeight),
                    Screen.fullScreen);
            }
        }

        private static void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private static Text CreateText(string name, Transform parent, string value, int size, FontStyle style)
        {
            var rt = CreateRect(name, parent);
            var text = rt.gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
