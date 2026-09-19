using Bayou.Environment;
using Bayou.Inventory.Shop;
using Bayou.Player;
using Bayou.Save;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
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
            "I  Inventory  ·  J  Journal  ·  Esc close\n" +
            "E  Interact  ·  Space / click  Dialogue\n" +
            "R  Rotate item\n" +
            "Tab  Cycle tools\n" +
            "1 Rod · 2 Net · 3 Lantern · 0 None\n" +
            "Left click  Cast / scoop  ·  melee damages enemies if chased\n" +
            "Lantern lights fog  ·  stay in fog too long without it and you are pushed back\n" +
            "Campfire  E rest to save  ·  cook a fish there to heal\n" +
            "V  Volume";

        [SerializeField] private bool buildUiIfMissing = true;
        [SerializeField] private Canvas rootCanvas;
        [SerializeField] private TextMeshProUGUI questTitleLabel;
        [SerializeField] private TextMeshProUGUI questObjectiveLabel;
        [SerializeField] private TextMeshProUGUI controlsLabel;
        [SerializeField] private GameObject questPanel;
        [SerializeField] private GameObject controlsPanel;
        [SerializeField] private GameObject fogWarningPanel;
        [SerializeField] private TextMeshProUGUI fogWarningLabel;
        [SerializeField] private GameObject healthPanel;
        [SerializeField] private Image healthFill;
        [SerializeField] private TextMeshProUGUI healthLabel;
        [SerializeField] private GameObject deathPanel;
        [SerializeField] private TextMeshProUGUI deathLabel;
        [SerializeField] private bool hideWhenMenusOpen = true;
        [SerializeField] private bool showControlsLegend;

        private RectTransform _healthFillRect;
        private PlayerHealth _boundHealth;
        private static Sprite _uiWhiteSprite;
        private string _trackedQuestId;
        private string _objectiveCache = "";
        private bool _subscribed;
        private bool _wasDialogueOpen;
        private bool _wasTeachingMove;

        public static GameplayHud Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            // AfterSceneLoad only runs for the first loaded scene (MainMenu in builds).
            // Re-apply on every scene change so the HUD appears in-game, not on the menu.
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            ApplyForActiveScene();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => ApplyForActiveScene();

        private static void ApplyForActiveScene()
        {
            if (IsGameplayScene())
                EnsureInScene();
            else
                DestroyAllInScene();
        }

        private static bool IsGameplayScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid()) return false;

            var name = scene.name;
            if (string.Equals(name, "MainMenu", System.StringComparison.OrdinalIgnoreCase))
                return false;

            // Prefer a real gameplay marker; fall back to known play scenes.
            if (Object.FindFirstObjectByType<QuestManager>(FindObjectsInactive.Include) != null)
                return true;

            return string.Equals(name, "MovementTest", System.StringComparison.OrdinalIgnoreCase);
        }

        private static void EnsureInScene()
        {
            if (Object.FindFirstObjectByType<GameplayHud>(FindObjectsInactive.Include) == null)
            {
                var go = new GameObject("GameplayHud");
                go.AddComponent<GameplayHud>();
            }

            // Always ensure these — even if GameplayHud already exists in the scene.
            QuestMarkerHud.EnsureInScene();
            InteractionPromptHud.EnsureInScene();
            EquipmentHotwheel.EnsureInScene();
            OnboardingCoach.EnsureInScene();
            QuestJournalHud.EnsureInScene();
        }

        private static void DestroyAllInScene()
        {
            var existing = Object.FindObjectsByType<GameplayHud>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < existing.Length; i++)
            {
                if (existing[i] != null)
                    Object.Destroy(existing[i].gameObject);
            }

            var markers = Object.FindObjectsByType<QuestMarkerHud>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < markers.Length; i++)
            {
                if (markers[i] != null)
                    Object.Destroy(markers[i].gameObject);
            }

            var prompts = Object.FindObjectsByType<InteractionPromptHud>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < prompts.Length; i++)
            {
                if (prompts[i] != null)
                    Object.Destroy(prompts[i].gameObject);
            }

            var journals = Object.FindObjectsByType<QuestJournalHud>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < journals.Length; i++)
            {
                if (journals[i] != null)
                    Object.Destroy(journals[i].gameObject);
            }

            var coaches = Object.FindObjectsByType<OnboardingCoach>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < coaches.Length; i++)
            {
                if (coaches[i] != null)
                    Object.Destroy(coaches[i].gameObject);
            }
        }

        private void Awake()
        {
            Instance = this;
            if (buildUiIfMissing && (rootCanvas == null || questTitleLabel == null || controlsLabel == null))
                BuildUi();
            if (controlsPanel == null && rootCanvas != null)
            {
                var existing = rootCanvas.transform.Find("Controls");
                if (existing != null)
                    controlsPanel = existing.gameObject;
            }
            if (controlsPanel != null)
                controlsPanel.SetActive(showControlsLegend);
            RefreshFogWarning();
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
            BindHealth(null);
        }

        private void OnDestroy()
        {
            Unsubscribe();
            BindHealth(null);
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

            var teaching = OnboardingCoach.IsTeachingMove;
            if (teaching != _wasTeachingMove)
            {
                _wasTeachingMove = teaching;
                RefreshQuestFromManager();
            }

            // Re-poll occasionally — events can fire while the canvas is hidden during dialogue.
            if (!dialogueOpen && (Time.frameCount % 30 == 0))
                RefreshQuestFromManager();

            if (rootCanvas == null) return;

            if (hideWhenMenusOpen)
            {
                var show = !ShouldHideForMenus();
                if (rootCanvas.gameObject.activeSelf != show)
                    rootCanvas.gameObject.SetActive(show);
                if (!show) return;
            }

            RefreshFogWarning();
            RefreshHealthHud();
        }

        private void RefreshFogWarning()
        {
            if (fogWarningPanel == null && rootCanvas != null)
            {
                var existing = rootCanvas.transform.Find("FogWarning");
                if (existing != null)
                    fogWarningPanel = existing.gameObject;
            }

            var text = FogBarrier.WarningText;
            var show = !string.IsNullOrEmpty(text);
            if (fogWarningPanel != null && fogWarningPanel.activeSelf != show)
                fogWarningPanel.SetActive(show);
            if (show && fogWarningLabel != null)
                fogWarningLabel.text = text;
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
                if (string.IsNullOrEmpty(_trackedQuestId) || _trackedQuestId == quest.info.id)
                {
                    _trackedQuestId = quest.info.id;
                    ApplyQuest(quest);
                }
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

        public string TrackedQuestId => _trackedQuestId;

        public void TrackQuest(string questId)
        {
            _trackedQuestId = string.IsNullOrWhiteSpace(questId) ? null : questId.Trim();
            RefreshQuestFromManager();
        }

        private void RefreshQuestFromManager()
        {
            var manager = QuestManager.Resolve();
            if (manager != null &&
                !string.IsNullOrEmpty(_trackedQuestId) &&
                manager.TryGetQuest(_trackedQuestId, out var pinned) &&
                pinned != null &&
                pinned.IsActiveForHud)
            {
                ApplyQuest(pinned);
                return;
            }

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
                questTitleLabel.text = OnboardingCoach.IsTeachingMove ? "Welcome to the bayou" : "No active quest";
            if (questObjectiveLabel != null)
                questObjectiveLabel.text = OnboardingCoach.IsTeachingMove
                    ? "WASD = Move. Then talk to townsfolk."
                    : "Talk to townsfolk to begin.";
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
            if (QuestJournalHud.IsOpen) return true;
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

            controlsPanel = CreatePanel("Controls", canvasGo.transform,
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(24f, 24f), new Vector2(340f, 210f));
            controlsLabel = CreateTmp("ControlsText", controlsPanel.transform, ControlsText, 16f, FontStyles.Normal,
                TextAlignmentOptions.BottomLeft);
            StretchTmp(controlsLabel.rectTransform, 14f, 12f, 14f, 12f);
            controlsPanel.SetActive(showControlsLegend);

            fogWarningPanel = CreatePanel("FogWarning", canvasGo.transform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(560f, 64f));
            var fogRt = fogWarningPanel.GetComponent<RectTransform>();
            fogRt.pivot = new Vector2(0.5f, 1f);
            var fogImg = fogWarningPanel.GetComponent<Image>();
            if (fogImg != null)
                fogImg.color = new Color(0.12f, 0.1f, 0.16f, 0.78f);
            fogWarningLabel = CreateTmp("FogText", fogWarningPanel.transform,
                "", 20f, FontStyles.Bold, TextAlignmentOptions.Center);
            StretchTmp(fogWarningLabel.rectTransform, 16f, 10f, 16f, 10f);
            fogWarningPanel.SetActive(false);
        }

        private void RefreshHealthHud()
        {
            EnsureHealthUi();
            var health = PlayerHealth.Resolve();
            BindHealth(health);
            if (health == null)
            {
                if (healthPanel != null)
                    healthPanel.SetActive(false);
                if (deathPanel != null)
                    deathPanel.SetActive(false);
                return;
            }

            if (healthPanel != null && !healthPanel.activeSelf)
                healthPanel.SetActive(true);

            var amount = Mathf.Clamp01(health.Normalized);
            if (_healthFillRect != null)
            {
                _healthFillRect.anchorMin = Vector2.zero;
                _healthFillRect.anchorMax = new Vector2(amount, 1f);
                _healthFillRect.offsetMin = Vector2.zero;
                _healthFillRect.offsetMax = Vector2.zero;
                _healthFillRect.gameObject.SetActive(amount > 0.001f);
            }

            if (healthFill != null)
            {
                healthFill.type = Image.Type.Simple;
                healthFill.sprite = UiWhiteSprite();
                healthFill.color = amount <= 0.35f
                    ? new Color(0.82f, 0.18f, 0.16f, 0.95f)
                    : new Color(0.72f, 0.22f, 0.2f, 0.92f);
            }

            if (healthLabel != null)
                healthLabel.text = $"Health  {health.Current} / {health.Max}";

            if (deathPanel != null)
                deathPanel.SetActive(health.IsDying);
        }

        private void BindHealth(PlayerHealth health)
        {
            if (_boundHealth == health) return;
            if (_boundHealth != null)
                _boundHealth.Changed -= RefreshHealthHud;
            _boundHealth = health;
            if (_boundHealth != null)
                _boundHealth.Changed += RefreshHealthHud;
        }

        private static Sprite UiWhiteSprite()
        {
            if (_uiWhiteSprite != null) return _uiWhiteSprite;
            var tex = Texture2D.whiteTexture;
            _uiWhiteSprite = Sprite.Create(
                tex,
                new Rect(0f, 0f, tex.width, tex.height),
                new Vector2(0.5f, 0.5f),
                4f);
            _uiWhiteSprite.name = "BayouUiWhite";
            return _uiWhiteSprite;
        }

        private void EnsureHealthUi()
        {
            if (rootCanvas == null) return;

            if (healthPanel == null)
            {
                var existing = rootCanvas.transform.Find("Health");
                healthPanel = existing != null ? existing.gameObject : null;
            }

            if (healthPanel == null)
            {
                healthPanel = CreatePanel("Health", rootCanvas.transform,
                    new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -24f), new Vector2(220f, 64f));
                var rt = healthPanel.GetComponent<RectTransform>();
                rt.pivot = new Vector2(1f, 1f);

                var track = new GameObject("Track", typeof(RectTransform));
                track.transform.SetParent(healthPanel.transform, false);
                var trackRt = track.GetComponent<RectTransform>();
                StretchTmp(trackRt, 14f, 34f, 14f, 12f);
                var trackImg = track.AddComponent<Image>();
                trackImg.sprite = UiWhiteSprite();
                trackImg.color = new Color(0.12f, 0.08f, 0.08f, 0.85f);
                trackImg.raycastTarget = false;

                var fillGo = new GameObject("Fill", typeof(RectTransform));
                fillGo.transform.SetParent(track.transform, false);
                _healthFillRect = fillGo.GetComponent<RectTransform>();
                _healthFillRect.anchorMin = Vector2.zero;
                _healthFillRect.anchorMax = Vector2.one;
                _healthFillRect.offsetMin = Vector2.zero;
                _healthFillRect.offsetMax = Vector2.zero;
                healthFill = fillGo.AddComponent<Image>();
                healthFill.sprite = UiWhiteSprite();
                healthFill.color = new Color(0.72f, 0.22f, 0.2f, 0.92f);
                healthFill.raycastTarget = false;
                healthFill.type = Image.Type.Simple;

                healthLabel = CreateTmp("HealthText", healthPanel.transform, "Health  5 / 5", 18f, FontStyles.Bold,
                    TextAlignmentOptions.TopLeft);
                StretchTmp(healthLabel.rectTransform, 14f, 8f, 14f, 36f);
            }
            else
            {
                if (healthFill == null || _healthFillRect == null)
                {
                    var fill = healthPanel.transform.Find("Track/Fill");
                    if (fill != null)
                    {
                        healthFill = fill.GetComponent<Image>();
                        _healthFillRect = fill.GetComponent<RectTransform>();
                    }
                }

                if (healthLabel == null)
                {
                    var label = healthPanel.transform.Find("HealthText");
                    if (label != null)
                        healthLabel = label.GetComponent<TextMeshProUGUI>();
                }
            }

            if (deathPanel == null)
            {
                var existing = rootCanvas.transform.Find("Death");
                deathPanel = existing != null ? existing.gameObject : null;
            }

            if (deathPanel == null)
            {
                deathPanel = CreatePanel("Death", rootCanvas.transform,
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(520f, 90f));
                var rt = deathPanel.GetComponent<RectTransform>();
                rt.pivot = new Vector2(0.5f, 0.5f);
                var img = deathPanel.GetComponent<Image>();
                if (img != null)
                    img.color = new Color(0.08f, 0.04f, 0.04f, 0.82f);
                deathLabel = CreateTmp("DeathText", deathPanel.transform,
                    "You collapsed.", 22f, FontStyles.Bold, TextAlignmentOptions.Center);
                StretchTmp(deathLabel.rectTransform, 16f, 12f, 16f, 12f);
                deathPanel.SetActive(false);
            }
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
            // Builds strip missing fonts — always pin TMP default (LiberationSans SDF).
            if (TMP_Settings.defaultFontAsset != null)
                tmp.font = TMP_Settings.defaultFontAsset;
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
