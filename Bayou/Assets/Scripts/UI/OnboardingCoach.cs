#if !ENABLE_INPUT_SYSTEM
#error OnboardingCoach requires the New Input System.
#endif

using Bayou.Player;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Bayou.UI
{
    /// <summary>
    /// First-load movement coach. Shows a readable WASD prompt until the player walks.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OnboardingCoach : MonoBehaviour, IInteractionPromptSource
    {
        public static OnboardingCoach Instance { get; private set; }

        public static bool IsTeachingMove => Instance != null && Instance._teachingMove;

        [SerializeField] private bool buildUiIfMissing = true;

        private Canvas _canvas;
        private RectTransform _card;
        private bool _teachingMove = true;
        private float _movedSeconds;
        private float _shownAt = -1f;
        private BayouCharacterMotor _motor;

        public static OnboardingCoach EnsureInScene()
        {
            if (Instance != null) return Instance;
            var existing = Object.FindFirstObjectByType<OnboardingCoach>(FindObjectsInactive.Include);
            if (existing != null) return existing;
            var go = new GameObject("OnboardingCoach");
            return go.AddComponent<OnboardingCoach>();
        }

        private void Awake()
        {
            Instance = this;
            if (buildUiIfMissing)
                BuildUi();
            SetCardVisible(false);
        }

        private void OnEnable() => InteractionPromptBroker.Register(this);

        private void OnDisable() => InteractionPromptBroker.Unregister(this);

        private void OnDestroy()
        {
            InteractionPromptBroker.Unregister(this);
            if (Instance == this)
                Instance = null;
        }

        private void Update()
        {
            if (!_teachingMove)
            {
                SetCardVisible(false);
                return;
            }

            if (ShouldHide())
            {
                SetCardVisible(false);
                return;
            }

            if (_shownAt < 0f)
                _shownAt = Time.unscaledTime;
            if (Time.unscaledTime - _shownAt < 0.35f)
                return;

            SetCardVisible(true);

            if (IsMoving())
                _movedSeconds += Time.unscaledDeltaTime;
            if (_movedSeconds >= 0.35f)
            {
                _teachingMove = false;
                SetCardVisible(false);
            }
        }

        public bool TryGetInteractionPrompt(out InteractionPrompt prompt)
        {
            prompt = default;
            if (!_teachingMove || ShouldHide())
                return false;
            if (_shownAt < 0f || Time.unscaledTime - _shownAt < 0.35f)
                return false;
            prompt = new InteractionPrompt("", "WASD = Move", 180);
            return true;
        }

        private bool IsMoving()
        {
            if (_motor == null)
                _motor = PlayerLocator.Motor;
            if (_motor != null && _motor.HasMoveInput)
                return true;

            var kb = Keyboard.current;
            if (kb == null) return false;
            return kb.wKey.isPressed || kb.aKey.isPressed || kb.sKey.isPressed || kb.dKey.isPressed ||
                   kb.upArrowKey.isPressed || kb.downArrowKey.isPressed ||
                   kb.leftArrowKey.isPressed || kb.rightArrowKey.isPressed;
        }

        private static bool ShouldHide()
        {
            if (AudioSettings.IsOpen) return true;
            if (Bayou.Inventory.Shop.ShopUIController.ActiveShop != null &&
                Bayou.Inventory.Shop.ShopUIController.ActiveShop.IsOpen) return true;
            if (Bayou.Save.BonfireUIController.Active != null &&
                Bayou.Save.BonfireUIController.Active.IsOpen) return true;
            var dialogue = DialogueManager.GetInstance();
            return dialogue != null && dialogue.dialogueIsPlaying;
        }

        private void SetCardVisible(bool on)
        {
            if (_card != null && _card.gameObject.activeSelf != on)
                _card.gameObject.SetActive(on);
        }

        private void BuildUi()
        {
            var canvasGo = new GameObject("OnboardingCanvas");
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 21;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>().enabled = false;

            _card = CreateRect("MoveHint", canvasGo.transform);
            _card.anchorMin = _card.anchorMax = new Vector2(0.5f, 0f);
            _card.pivot = new Vector2(0.5f, 0f);
            _card.anchoredPosition = new Vector2(0f, 168f);
            _card.sizeDelta = new Vector2(460f, 78f);

            var bg = _card.gameObject.AddComponent<Image>();
            bg.color = new Color(0.04f, 0.05f, 0.04f, 0.78f);
            bg.raycastTarget = false;

            var line = CreateTmp("Action", _card, "WASD = Move", 28f, FontStyles.Bold);
            line.rectTransform.anchorMin = Vector2.zero;
            line.rectTransform.anchorMax = Vector2.one;
            line.rectTransform.offsetMin = new Vector2(18f, 8f);
            line.rectTransform.offsetMax = new Vector2(-18f, -8f);
            line.alignment = TextAlignmentOptions.Center;
            line.color = new Color(0.96f, 0.86f, 0.42f, 1f);
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        private static TextMeshProUGUI CreateTmp(string name, Transform parent, string text, float size, FontStyles style)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(0.94f, 0.95f, 0.9f, 1f);
            tmp.raycastTarget = false;
            if (TMP_Settings.defaultFontAsset != null)
                tmp.font = TMP_Settings.defaultFontAsset;
            return tmp;
        }
    }
}
