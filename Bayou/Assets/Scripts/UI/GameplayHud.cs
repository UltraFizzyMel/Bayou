using Bayou.Inventory.Shop;
using Bayou.Save;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Bayou.UI
{
    /// <summary>
    /// Always-on gameplay HUD: control legend + active quest log.
    /// Works in player builds without PlaytestHarness.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameplayHud : MonoBehaviour
    {
        private const string ControlsText =
            "<b>Controls</b>\n" +
            "I  Inventory\n" +
            "E  Interact\n" +
            "R  Rotate item\n" +
            "Tab  Cycle tools\n" +
            "1 Rod · 2 Net · 3 Lantern · 0 None\n" +
            "Cast / scoop with held tool\n" +
            "Esc / Q  Cancel cast\n" +
            "V  Volume";

        [SerializeField] private bool buildUiIfMissing = true;
        [SerializeField] private Canvas rootCanvas;
        [SerializeField] private TextMeshProUGUI questTitleLabel;
        [SerializeField] private TextMeshProUGUI questObjectiveLabel;
        [SerializeField] private TextMeshProUGUI controlsLabel;
        [SerializeField] private GameObject questPanel;
        [SerializeField] private bool hideWhenMenusOpen = true;

        private string _trackedQuestId;
        private string _objectiveCache = "";
        private bool _subscribed;
        private bool _wasDialogueOpen;

        public static GameplayHud Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureInScene()
        {
            if (Object.FindFirstObjectByType<GameplayHud>(FindObjectsInactive.Include) != null)
                return;

            var go = new GameObject("GameplayHud");
            go.AddComponent<GameplayHud>();
        }

        private void Awake()
        {
            Instance = this;
            if (buildUiIfMissing && (rootCanvas == null || questTitleLabel == null || controlsLabel == null))
                BuildUi();
        }

        private void OnEnable()
        {
            _subscribed = false;
            TrySubscribe();
            RefreshQuestFromManager();
        }

        private void OnDisable()
        {
            Unsubscribe();
            _subscribed = false;
        }

        private void OnDestroy()
        {
            Unsubscribe();
            if (Instance == this)
                Instance = null;
        }

        private void LateUpdate()
        {
            if (!_subscribed)
                TrySubscribe();

            var dialogueOpen = IsDialogueOpen();
            if (_wasDialogueOpen && !dialogueOpen)
                RefreshQuestFromManager();
            _wasDialogueOpen = dialogueOpen;

            // Re-poll occasionally — events can fire while the canvas is hidden during dialogue.
            if (!dialogueOpen && (Time.frameCount % 30 == 0))
                RefreshQuestFromManager();

            if (!hideWhenMenusOpen || rootCanvas == null) return;
            var show = !ShouldHideForMenus();
            if (rootCanvas.gameObject.activeSelf != show)
                rootCanvas.gameObject.SetActive(show);
        }

        private void TrySubscribe()
        {
            var events = GameEventManager.Instance != null ? GameEventManager.Instance.questEvents : null;
            if (events == null || _subscribed) return;

            events.onQuestStateChange -= OnQuestStateChange;
            events.onQuestStepStateChange -= OnQuestStepStateChange;
            events.onQuestStateChange += OnQuestStateChange;
            events.onQuestStepStateChange += OnQuestStepStateChange;
            _subscribed = true;
            RefreshQuestFromManager();
        }

        private void Unsubscribe()
        {
            var events = GameEventManager.Instance != null ? GameEventManager.Instance.questEvents : null;
            if (events == null) return;

            events.onQuestStateChange -= OnQuestStateChange;
            events.onQuestStepStateChange -= OnQuestStepStateChange;
        }

        private void OnQuestStateChange(Quest quest)
        {
            if (quest?.info == null) return;

            if (quest.IsActiveForHud)
            {
                // Prefer the quest that just became active (Caliste shop quest, etc.).
                _trackedQuestId = quest.info.id;
                ApplyQuest(quest);
                return;
            }

            if (_trackedQuestId == quest.info.id)
            {
                _trackedQuestId = null;
                RefreshQuestFromManager();
            }
        }

        private void OnQuestStepStateChange(string id, int stepIndex, QuestStepState stepState)
        {
            if (string.IsNullOrEmpty(id)) return;

            // Adopt this quest if we have none yet, or it matches the tracked one.
            if (_trackedQuestId != null && id != _trackedQuestId)
                return;

            _trackedQuestId = id;
            if (stepState != null && !string.IsNullOrWhiteSpace(stepState.state))
                _objectiveCache = stepState.state;

            var manager = QuestManager.Resolve();
            if (manager != null && manager.TryGetQuest(id, out var quest) && quest.IsActiveForHud)
            {
                ApplyQuest(quest);
                if (!string.IsNullOrWhiteSpace(_objectiveCache) && questObjectiveLabel != null)
                    questObjectiveLabel.text = _objectiveCache;
                return;
            }

            if (questTitleLabel != null && string.IsNullOrWhiteSpace(questTitleLabel.text))
                questTitleLabel.text = id;

            if (questObjectiveLabel != null)
                questObjectiveLabel.text = string.IsNullOrWhiteSpace(_objectiveCache)
                    ? "In progress"
                    : _objectiveCache;

            ShowQuestPanel(true);
        }

        private void RefreshQuestFromManager()
        {
            var manager = QuestManager.Resolve();
            if (manager != null && manager.TryGetPrimaryActiveQuest(out var quest))
            {
                _trackedQuestId = quest.info != null ? quest.info.id : null;
                ApplyQuest(quest);
                return;
            }

            _trackedQuestId = null;
            ApplyEmptyQuest();
        }

        private void ApplyQuest(Quest quest)
        {
            if (quest?.info == null)
            {
                ApplyEmptyQuest();
                return;
            }

            if (questTitleLabel != null)
                questTitleLabel.text = quest.info.displayName;

            var objective = quest.GetHudObjectiveText();
            if (string.IsNullOrWhiteSpace(objective) && !string.IsNullOrWhiteSpace(_objectiveCache) &&
                _trackedQuestId == quest.info.id)
                objective = _objectiveCache;

            _objectiveCache = objective;
            if (questObjectiveLabel != null)
                questObjectiveLabel.text = string.IsNullOrWhiteSpace(objective)
                    ? "In progress"
                    : objective;

            ShowQuestPanel(true);
        }

        private void ApplyEmptyQuest()
        {
            if (questTitleLabel != null)
                questTitleLabel.text = "No active quest";
            if (questObjectiveLabel != null)
                questObjectiveLabel.text = "Talk to townsfolk to begin.";
            ShowQuestPanel(true);
        }

        private void ShowQuestPanel(bool visible)
        {
            if (questPanel != null)
                questPanel.SetActive(visible);
        }

        private static bool ShouldHideForMenus()
        {
            if (AudioSettings.IsOpen) return true;
            if (ShopUIController.ActiveShop != null && ShopUIController.ActiveShop.IsOpen) return true;
            if (BonfireUIController.Active != null && BonfireUIController.Active.IsOpen) return true;
            if (IsDialogueOpen()) return true;
            return false;
        }

        private static bool IsDialogueOpen()
        {
            var dialogue = DialogueManager.GetInstance();
            return dialogue != null && dialogue.dialogueIsPlaying;
        }

        private void BuildUi()
        {
            var canvasGo = new GameObject("GameplayHudCanvas");
            canvasGo.transform.SetParent(transform, false);
            rootCanvas = canvasGo.AddComponent<Canvas>();
            rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            rootCanvas.sortingOrder = 8;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>().enabled = false;

            questPanel = CreatePanel("QuestLog", canvasGo.transform,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -24f), new Vector2(360f, 120f));
            questTitleLabel = CreateTmp("QuestTitle", questPanel.transform, "No active quest", 22f, FontStyles.Bold,
                TextAlignmentOptions.TopLeft);
            StretchTmp(questTitleLabel.rectTransform, 14f, 10f, 14f, 58f);
            questObjectiveLabel = CreateTmp("QuestObjective", questPanel.transform, "Talk to townsfolk to begin.", 18f,
                FontStyles.Normal, TextAlignmentOptions.TopLeft);
            StretchTmp(questObjectiveLabel.rectTransform, 14f, 48f, 14f, 14f);

            var controlsPanel = CreatePanel("Controls", canvasGo.transform,
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(24f, 24f), new Vector2(340f, 210f));
            controlsLabel = CreateTmp("ControlsText", controlsPanel.transform, ControlsText, 16f, FontStyles.Normal,
                TextAlignmentOptions.BottomLeft);
            StretchTmp(controlsLabel.rectTransform, 14f, 12f, 14f, 12f);
        }

        private static GameObject CreatePanel(
            string name,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 anchoredPos,
            Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = anchorMin;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;

            var img = go.AddComponent<Image>();
            img.color = new Color(0.05f, 0.07f, 0.06f, 0.55f);
            img.raycastTarget = false;
            return go;
        }

        private static TextMeshProUGUI CreateTmp(
            string name,
            Transform parent,
            string text,
            float size,
            FontStyles style,
            TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.alignment = align;
            tmp.color = new Color(0.92f, 0.94f, 0.9f, 0.95f);
            tmp.raycastTarget = false;
            tmp.enableWordWrapping = true;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            return tmp;
        }

        private static void StretchTmp(RectTransform rt, float left, float top, float right, float bottom)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }
    }
}
