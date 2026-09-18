using System.Collections.Generic;
using Bayou;
using Bayou.Inventory.Shop;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Bayou.UI
{
    /// <summary>
    /// Full quest journal (J). Lists active / available / completed quests and pins tracking.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(80)]
    public sealed class QuestJournalHud : MonoBehaviour, IInteractionPromptSource
    {
        private const string HiddenQuestId = "CollectFishExampleQuest";
        private const string OpenedPrefKey = "Bayou.HasOpenedQuestJournal";
        private const float HintDelay = 0.6f;

        public static QuestJournalHud Instance { get; private set; }
        public static bool IsOpen => Instance != null && Instance._isOpen;

        [SerializeField] private bool buildUiIfMissing = true;

        private Canvas _canvas;
        private GameObject _root;
        private RectTransform _listContent;
        private TextMeshProUGUI _detailTitle;
        private TextMeshProUGUI _detailStatus;
        private TextMeshProUGUI _detailBody;
        private TextMeshProUGUI _trackLabel;
        private bool _isOpen;
        private bool _subscribed;
        private string _selectedId;
        private readonly List<Quest> _scratch = new();
        private RectTransform _hintCard;
        private bool _hintArmed;
        private bool _hintDone;
        private float _hintReadyAt = -1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            // Spawned from GameplayHud.EnsureInScene — keep a fallback if HUD is missing.
        }

        public static QuestJournalHud EnsureInScene()
        {
            if (Instance != null) return Instance;
            var existing = Object.FindFirstObjectByType<QuestJournalHud>(FindObjectsInactive.Include);
            if (existing != null) return existing;
            var go = new GameObject("QuestJournalHud");
            return go.AddComponent<QuestJournalHud>();
        }

        private void Awake()
        {
            Instance = this;
            if (buildUiIfMissing && _canvas == null)
                BuildUi();
            SetOpen(false, syncPause: false);
        }

        private void OnEnable()
        {
            _subscribed = false;
            TrySubscribe();
            InteractionPromptBroker.Register(this);
            if (PlayerPrefs.GetInt(OpenedPrefKey, 0) != 0)
                _hintDone = true;
        }

        private void OnDisable()
        {
            Unsubscribe();
            _subscribed = false;
            InteractionPromptBroker.Unregister(this);
        }

        private void OnDestroy()
        {
            Unsubscribe();
            InteractionPromptBroker.Unregister(this);
            if (Instance == this)
                Instance = null;
            if (_isOpen)
            {
                _isOpen = false;
                GameplayPause.SyncFromUiState();
            }
        }

        private void Update()
        {
            if (!_subscribed)
                TrySubscribe();

            if (WasTogglePressed() && CanToggle())
            {
                SetOpen(!_isOpen);
                return;
            }

            if (_isOpen && WasClosePressed())
                SetOpen(false);

            TickFirstQuestHint();
        }

        public void Toggle()
        {
            if (!CanToggle() && !_isOpen) return;
            SetOpen(!_isOpen);
        }

        public void Close() => SetOpen(false);

        private void SetOpen(bool open, bool syncPause = true)
        {
            _isOpen = open;
            if (_root != null && _root.activeSelf != open)
                _root.SetActive(open);
            if (_canvas != null)
                _canvas.enabled = open;

            if (open)
            {
                EnsureEventSystem();
                Refresh();
                DismissFirstQuestHint();
            }

            if (syncPause)
                GameplayPause.SyncFromUiState();
        }

        private static bool CanToggle()
        {
            var dialogue = DialogueManager.GetInstance();
            if (dialogue != null && dialogue.dialogueIsPlaying) return false;
            if (AudioSettings.IsOpen) return false;
            if (Bayou.Inventory.Shop.ShopUIController.ActiveShop != null &&
                Bayou.Inventory.Shop.ShopUIController.ActiveShop.IsOpen) return false;
            if (Bayou.Save.BonfireUIController.Active != null &&
                Bayou.Save.BonfireUIController.Active.IsOpen) return false;
            return true;
        }

        private static bool WasTogglePressed()
        {
            var kb = Keyboard.current;
            return kb != null && kb.jKey.wasPressedThisFrame;
        }

        private static bool WasClosePressed()
        {
            var kb = Keyboard.current;
            return kb != null && kb.escapeKey.wasPressedThisFrame;
        }

        private void TrySubscribe()
        {
            var events = GameEventManager.Instance != null ? GameEventManager.Instance.questEvents : null;
            if (events == null || _subscribed) return;
            events.onQuestStateChange += OnQuestChanged;
            events.onQuestStepStateChange += OnQuestStepChanged;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            var events = GameEventManager.Instance != null ? GameEventManager.Instance.questEvents : null;
            if (events == null) return;
            events.onQuestStateChange -= OnQuestChanged;
            events.onQuestStepStateChange -= OnQuestStepChanged;
        }

        private void OnQuestChanged(Quest quest)
        {
            if (_isOpen) Refresh();
            if (quest != null && quest.IsActiveForHud)
                ArmFirstQuestHint();
        }

        private void OnQuestStepChanged(string id, int step, QuestStepState state)
        {
            if (_isOpen) Refresh();
        }

        public bool TryGetInteractionPrompt(out InteractionPrompt prompt)
        {
            prompt = default;
            if (!IsHintVisible()) return false;
            prompt = new InteractionPrompt("J", "Open journal", 175);
            return true;
        }

        private void ArmFirstQuestHint()
        {
            if (_hintDone || _hintArmed) return;
            if (PlayerPrefs.GetInt(OpenedPrefKey, 0) != 0)
            {
                _hintDone = true;
                return;
            }

            _hintArmed = true;
            _hintReadyAt = -1f;
        }

        private void DismissFirstQuestHint()
        {
            _hintDone = true;
            _hintArmed = false;
            SetHintVisible(false);
            PlayerPrefs.SetInt(OpenedPrefKey, 1);
            PlayerPrefs.Save();
        }

        private void TickFirstQuestHint()
        {
            if (_hintDone)
            {
                SetHintVisible(false);
                return;
            }

            if (!_hintArmed && HasActiveQuest())
                ArmFirstQuestHint();

            if (!IsHintVisible())
            {
                SetHintVisible(false);
                return;
            }

            SetHintVisible(true);
        }

        private bool IsHintVisible()
        {
            if (_hintDone || !_hintArmed || _isOpen) return false;
            if (OnboardingCoach.IsTeachingMove) return false;
            if (ShouldHideHint()) return false;

            if (_hintReadyAt < 0f)
                _hintReadyAt = Time.unscaledTime + HintDelay;
            return Time.unscaledTime >= _hintReadyAt;
        }

        private static bool ShouldHideHint()
        {
            if (AudioSettings.IsOpen) return true;
            if (Bayou.Inventory.Shop.ShopUIController.ActiveShop != null &&
                Bayou.Inventory.Shop.ShopUIController.ActiveShop.IsOpen) return true;
            if (Bayou.Save.BonfireUIController.Active != null &&
                Bayou.Save.BonfireUIController.Active.IsOpen) return true;
            var dialogue = DialogueManager.GetInstance();
            return dialogue != null && dialogue.dialogueIsPlaying;
        }

        private static bool HasActiveQuest()
        {
            var manager = QuestManager.Resolve();
            if (manager == null) return false;
            foreach (var quest in manager.AllQuests)
            {
                if (quest != null && quest.IsActiveForHud && ShouldShow(quest))
                    return true;
            }

            return false;
        }

        private void SetHintVisible(bool on)
        {
            if (_hintCard != null && _hintCard.gameObject.activeSelf != on)
                _hintCard.gameObject.SetActive(on);
        }

        private void Refresh()
        {
            if (_listContent == null) return;

            for (var i = _listContent.childCount - 1; i >= 0; i--)
                Destroy(_listContent.GetChild(i).gameObject);

            var manager = QuestManager.Resolve();
            if (manager == null)
            {
                ApplyDetail(null);
                return;
            }

            Collect(manager, QuestState.IN_PROGRESS, QuestState.CAN_FINISH);
            var active = Snapshot();
            Collect(manager, QuestState.CAN_START);
            var available = Snapshot();
            Collect(manager, QuestState.FINISHED);
            var done = Snapshot();

            if (active.Count > 0) AddHeader("Active");
            foreach (var q in active) AddRow(q);
            if (available.Count > 0) AddHeader("Available");
            foreach (var q in available) AddRow(q);
            if (done.Count > 0) AddHeader("Completed");
            foreach (var q in done) AddRow(q);

            if (active.Count == 0 && available.Count == 0 && done.Count == 0)
                AddHeader("No quests yet — talk to townsfolk.");

            if (string.IsNullOrEmpty(_selectedId) || !manager.TryGetQuest(_selectedId, out _))
            {
                if (active.Count > 0) _selectedId = active[0].info.id;
                else if (available.Count > 0) _selectedId = available[0].info.id;
                else if (done.Count > 0) _selectedId = done[0].info.id;
            }

            manager.TryGetQuest(_selectedId, out var selected);
            ApplyDetail(selected);
            HighlightRows();
        }

        private void Collect(QuestManager manager, params QuestState[] states)
        {
            _scratch.Clear();
            foreach (var quest in manager.AllQuests)
            {
                if (!ShouldShow(quest)) continue;
                for (var i = 0; i < states.Length; i++)
                {
                    if (quest.state == states[i])
                    {
                        _scratch.Add(quest);
                        break;
                    }
                }
            }

            _scratch.Sort(CompareQuests);
        }

        private List<Quest> Snapshot()
        {
            var copy = new List<Quest>(_scratch.Count);
            copy.AddRange(_scratch);
            return copy;
        }

        private static bool ShouldShow(Quest quest)
        {
            if (quest?.info == null) return false;
            if (string.Equals(quest.info.id, HiddenQuestId, System.StringComparison.OrdinalIgnoreCase))
                return false;
            if (string.IsNullOrWhiteSpace(quest.info.displayName)) return false;
            return true;
        }

        private static int CompareQuests(Quest a, Quest b)
        {
            var an = a?.info != null ? a.info.displayName : "";
            var bn = b?.info != null ? b.info.displayName : "";
            return string.Compare(an, bn, System.StringComparison.OrdinalIgnoreCase);
        }

        private void AddHeader(string text)
        {
            var tmp = CreateTmp("Header", _listContent, text, 16f, FontStyles.Bold,
                TextAlignmentOptions.MidlineLeft, ShopUiStyle.TextCream);
            var le = tmp.gameObject.AddComponent<LayoutElement>();
            le.minHeight = 32f;
            le.preferredHeight = 32f;
            var rt = tmp.rectTransform;
            rt.offsetMin = new Vector2(8f, 0f);
            rt.offsetMax = new Vector2(-8f, 0f);
        }

        private void AddRow(Quest quest)
        {
            var id = quest.info.id;
            var go = new GameObject("Row_" + id, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(_listContent, false);
            var le = go.GetComponent<LayoutElement>();
            le.minHeight = 58f;
            le.preferredHeight = 58f;
            var img = go.GetComponent<Image>();
            img.color = RowColor(quest, selected: false);
            img.raycastTarget = true;
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => SelectQuest(id));

            var tag = go.AddComponent<QuestJournalRow>();
            tag.QuestId = id;

            var title = quest.info.displayName;
            var line2 = quest.GetJournalStatusLabel() + "  ·  " + quest.GetHudObjectiveText();
            if (string.IsNullOrWhiteSpace(quest.GetHudObjectiveText()) ||
                quest.state == QuestState.FINISHED)
                line2 = quest.GetJournalStatusLabel();
            if (quest.state == QuestState.CAN_START)
                line2 = "Available";

            var titleTmp = CreateTmp("Title", go.transform, title, 18f, FontStyles.Bold,
                TextAlignmentOptions.TopLeft, ShopUiStyle.TextCream);
            Stretch(titleTmp.rectTransform, 14f, 8f, 14f, 28f);
            var sub = CreateTmp("Sub", go.transform, line2, 14f, FontStyles.Normal,
                TextAlignmentOptions.TopLeft, new Color(0.82f, 0.76f, 0.64f, 0.95f));
            Stretch(sub.rectTransform, 14f, 30f, 14f, 6f);
        }

        private void HighlightRows()
        {
            var tracked = GameplayHud.Instance != null ? GameplayHud.Instance.TrackedQuestId : null;
            for (var i = 0; i < _listContent.childCount; i++)
            {
                var row = _listContent.GetChild(i).GetComponent<QuestJournalRow>();
                if (row == null) continue;
                var img = row.GetComponent<Image>();
                if (img == null) continue;
                var manager = QuestManager.Resolve();
                if (manager == null || !manager.TryGetQuest(row.QuestId, out var quest)) continue;
                img.color = RowColor(quest, row.QuestId == _selectedId);
                if (row.QuestId == tracked && quest.IsActiveForHud)
                {
                    var sub = row.transform.Find("Sub")?.GetComponent<TextMeshProUGUI>();
                    if (sub != null && sub.text.IndexOf("Tracking", System.StringComparison.OrdinalIgnoreCase) < 0)
                        sub.text = "Tracking  ·  " + sub.text;
                }
            }
        }

        private Color RowColor(Quest quest, bool selected)
        {
            if (selected) return ShopUiStyle.ButtonGreen;
            if (quest != null && quest.IsActiveForHud) return ShopUiStyle.HeaderFooter;
            if (quest != null && quest.state == QuestState.FINISHED)
                return new Color(0.28f, 0.22f, 0.15f, 0.72f);
            return ShopUiStyle.ButtonMuted;
        }

        private void SelectQuest(string questId)
        {
            _selectedId = questId;
            var manager = QuestManager.Resolve();
            if (manager != null && manager.TryGetQuest(questId, out var quest))
            {
                if (quest.IsActiveForHud)
                    GameplayHud.Instance?.TrackQuest(questId);
                ApplyDetail(quest);
            }
            HighlightRows();
        }

        private void TrackSelected()
        {
            if (string.IsNullOrEmpty(_selectedId)) return;
            var manager = QuestManager.Resolve();
            if (manager == null || !manager.TryGetQuest(_selectedId, out var quest)) return;
            if (!quest.IsActiveForHud) return;
            GameplayHud.Instance?.TrackQuest(_selectedId);
            HighlightRows();
            ApplyDetail(quest);
        }

        private void ApplyDetail(Quest quest)
        {
            if (_detailTitle == null) return;
            if (quest?.info == null)
            {
                _detailTitle.text = "Quest Journal";
                _detailStatus.text = "";
                _detailBody.text = "Talk to townsfolk to pick up work around the bayou.";
                if (_trackLabel != null) _trackLabel.text = "";
                return;
            }

            _detailTitle.text = quest.info.displayName;
            _detailStatus.text = quest.GetJournalStatusLabel();
            _detailStatus.color = StatusColor(quest.state);

            var body = quest.GetHudObjectiveText();
            if (quest.state == QuestState.CAN_START)
                body = "This job is ready to take. Progress will show here once it is underway.";
            else if (quest.state == QuestState.FINISHED)
                body = "Completed.";
            else if (string.IsNullOrWhiteSpace(body))
                body = "In progress.";

            if (!string.IsNullOrWhiteSpace(quest.info.MiscReward))
                body += "\n\nReward: " + quest.info.MiscReward.Trim();

            var tracked = GameplayHud.Instance != null &&
                          string.Equals(GameplayHud.Instance.TrackedQuestId, quest.info.id,
                              System.StringComparison.OrdinalIgnoreCase);
            if (quest.IsActiveForHud)
                body += tracked
                    ? "\n\nThis quest is pinned on the compass."
                    : "\n\nClick the entry again, or Track, to pin it.";

            _detailBody.text = body;
            if (_trackLabel != null)
                _trackLabel.text = quest.IsActiveForHud ? (tracked ? "Tracking" : "Click to track") : "";
        }

        private static Color StatusColor(QuestState state) => state switch
        {
            QuestState.CAN_FINISH => new Color(0.86f, 0.78f, 0.42f, 1f),
            QuestState.IN_PROGRESS => ShopUiStyle.HoverValid,
            QuestState.FINISHED => new Color(0.72f, 0.64f, 0.52f, 1f),
            QuestState.CAN_START => ShopUiStyle.CellCream,
            _ => ShopUiStyle.TextCream
        };

        private void BuildUi()
        {
            var canvasGo = new GameObject("QuestJournalCanvas");
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 55;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            _root = new GameObject("Root", typeof(RectTransform));
            _root.transform.SetParent(canvasGo.transform, false);
            StretchFull(_root.GetComponent<RectTransform>());

            var dimGo = new GameObject("Dim", typeof(RectTransform), typeof(Image));
            dimGo.transform.SetParent(_root.transform, false);
            StretchFull(dimGo.GetComponent<RectTransform>());
            var dim = dimGo.GetComponent<Image>();
            dim.color = ShopUiStyle.OverlayDim;
            dim.raycastTarget = true;

            var panel = CreatePanel("Panel", _root.transform, new Vector2(0.5f, 0.5f), new Vector2(1080f, 680f));
            panel.GetComponent<Image>().color = ShopUiStyle.PanelBrown;

            var header = CreateTmp("Header", panel.transform, "Quest Journal", 28f, FontStyles.Bold,
                TextAlignmentOptions.MidlineLeft, ShopUiStyle.TextCream);
            Pin(header.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(28f, -58f), new Vector2(-200f, -16f));

            var hint = CreateTmp("Hint", panel.transform, "J / Esc close", 16f, FontStyles.Normal,
                TextAlignmentOptions.MidlineRight, new Color(0.82f, 0.76f, 0.64f, 0.95f));
            Pin(hint.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-200f, -58f), new Vector2(-24f, -16f));

            var listPanel = CreatePanel("List", panel.transform, new Vector2(0.5f, 0.5f), new Vector2(100f, 100f));
            listPanel.GetComponent<Image>().color = ShopUiStyle.HeaderFooter;
            Pin(listPanel.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0.46f, 1f),
                new Vector2(20f, 20f), new Vector2(-8f, -70f));

            var scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect), typeof(RectMask2D));
            scrollGo.transform.SetParent(listPanel.transform, false);
            StretchFull(scrollGo.GetComponent<RectTransform>(), 6f);
            scrollGo.GetComponent<Image>().color = new Color(0.12f, 0.08f, 0.05f, 0.22f);
            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 28f;

            var contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentGo.transform.SetParent(scrollGo.transform, false);
            _listContent = contentGo.GetComponent<RectTransform>();
            _listContent.anchorMin = new Vector2(0f, 1f);
            _listContent.anchorMax = new Vector2(1f, 1f);
            _listContent.pivot = new Vector2(0.5f, 1f);
            _listContent.anchoredPosition = Vector2.zero;
            _listContent.sizeDelta = new Vector2(0f, 0f);
            var vlg = contentGo.GetComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(6, 6, 6, 6);
            vlg.spacing = 6f;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlHeight = true;
            vlg.childControlWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childForceExpandWidth = true;
            var fitter = contentGo.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = _listContent;
            scroll.viewport = scrollGo.GetComponent<RectTransform>();

            var detail = CreatePanel("Detail", panel.transform, new Vector2(0.5f, 0.5f), new Vector2(100f, 100f));
            detail.GetComponent<Image>().color = ShopUiStyle.HeaderFooter;
            Pin(detail.GetComponent<RectTransform>(), new Vector2(0.46f, 0f), new Vector2(1f, 1f),
                new Vector2(8f, 20f), new Vector2(-20f, -70f));

            _detailTitle = CreateTmp("DetailTitle", detail.transform, "Quest Journal", 26f, FontStyles.Bold,
                TextAlignmentOptions.TopLeft, ShopUiStyle.TextCream);
            Pin(_detailTitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(22f, -68f), new Vector2(-22f, -18f));

            _detailStatus = CreateTmp("DetailStatus", detail.transform, "", 18f, FontStyles.Bold,
                TextAlignmentOptions.TopLeft, ShopUiStyle.HoverValid);
            Pin(_detailStatus.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(22f, -100f), new Vector2(-22f, -70f));

            _detailBody = CreateTmp("DetailBody", detail.transform, "", 18f, FontStyles.Normal,
                TextAlignmentOptions.TopLeft, ShopUiStyle.CellCream);
            Pin(_detailBody.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f),
                new Vector2(22f, 64f), new Vector2(-22f, -112f));

            var trackBtnGo = new GameObject("TrackButton", typeof(RectTransform), typeof(Image), typeof(Button));
            trackBtnGo.transform.SetParent(detail.transform, false);
            Pin(trackBtnGo.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(22f, 18f), new Vector2(-22f, 54f));
            var trackImg = trackBtnGo.GetComponent<Image>();
            trackImg.color = ShopUiStyle.ButtonGreen;
            var trackBtn = trackBtnGo.GetComponent<Button>();
            trackBtn.targetGraphic = trackImg;
            trackBtn.onClick.AddListener(TrackSelected);
            _trackLabel = CreateTmp("TrackLabel", trackBtnGo.transform, "Click to track", 18f, FontStyles.Bold,
                TextAlignmentOptions.Center, ShopUiStyle.TextCream);
            Stretch(_trackLabel.rectTransform, 0f, 0f, 0f, 0f);

            BuildHintUi();
        }

        private void BuildHintUi()
        {
            var canvasGo = new GameObject("JournalHintCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 22;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>().enabled = false;

            var cardGo = new GameObject("JournalHint", typeof(RectTransform), typeof(Image));
            cardGo.transform.SetParent(canvasGo.transform, false);
            _hintCard = cardGo.GetComponent<RectTransform>();
            _hintCard.anchorMin = _hintCard.anchorMax = new Vector2(0.5f, 0f);
            _hintCard.pivot = new Vector2(0.5f, 0f);
            _hintCard.anchoredPosition = new Vector2(0f, 168f);
            _hintCard.sizeDelta = new Vector2(520f, 78f);
            var bg = cardGo.GetComponent<Image>();
            bg.color = ShopUiStyle.HeaderFooter;
            bg.raycastTarget = false;

            var line = CreateTmp("HintText", cardGo.transform, "[J]  Open journal", 28f, FontStyles.Bold,
                TextAlignmentOptions.Center, ShopUiStyle.TextCream);
            Stretch(line.rectTransform, 18f, 8f, 18f, 8f);
            cardGo.SetActive(false);
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
            go.AddComponent<BayouUiInputBootstrap>();
        }

        private static GameObject CreatePanel(string name, Transform parent, Vector2 pivot, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = pivot;
            rt.sizeDelta = size;
            return go;
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
            tmp.enableWordWrapping = true;
            tmp.overflowMode = TextOverflowModes.Overflow;
            if (TMP_Settings.defaultFontAsset != null)
                tmp.font = TMP_Settings.defaultFontAsset;
            return tmp;
        }

        private static void StretchFull(RectTransform rt, float inset = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
        }

        private static void Stretch(RectTransform rt, float left, float top, float right, float bottom)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        private static void Pin(RectTransform rt, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
        }

        private sealed class QuestJournalRow : MonoBehaviour
        {
            public string QuestId;
        }
    }
}
