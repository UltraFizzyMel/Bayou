using System.Collections.Generic;
using Bayou.Creatures;
using Bayou.Inventory;
using Bayou.Inventory.Shop;
using Bayou.Save;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Bayou.UI
{
    /// <summary>
    /// One-time lesson cards. PlayerPrefs key Bayou.Tip.&lt;id&gt; so a lesson does not repeat.
    /// Only the combat card pauses the game.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-50)]
    public sealed class TutorialTips : MonoBehaviour
    {
        private const string Prefix = "Bayou.Tip.";

        private enum TipDismiss
        {
            Button,
            InventoryClose,
            ShopClose,
            JournalCycle,
            WheelRelease,
            CampfireUi
        }

        private sealed class Tip
        {
            public string Id;
            public string Body;
            public TipDismiss Dismiss;
            public bool Pause;
        }

        public static TutorialTips Instance { get; private set; }

        private readonly List<Tip> _queue = new();
        private Tip _shown;
        private Canvas _canvas;
        private GameObject _panel;
        private TextMeshProUGUI _body;
        private bool _fishBaseline;
        private int _fishCount;
        private bool _fishWaiting;
        private bool _journalRequested;
        private bool _campfireRequested;
        private bool _inventoryWasOpen;
        private bool _shopWasOpen;
        private bool _journalWasOpen;
        private bool _campfireUiWasOpen;
        private float _nextCampfireScan;
        private BonfireInteractable[] _fires = System.Array.Empty<BonfireInteractable>();
        private Vector3 _tutorialPond;
        private bool _tutorialPondKnown;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (Instance != null) return;
            var go = new GameObject("TutorialTips");
            DontDestroyOnLoad(go);
            go.AddComponent<TutorialTips>();
        }

        public static void NotifyInventoryClosed() => Instance?.OnInventoryClosed();
        public static void NotifyShopOpened() => Instance?.RequestShop();
        public static void NotifyShopClosed() => Instance?.OnShopClosed();
        public static void NotifyJournalOpened() => Instance?.OnJournalOpened();
        public static void NotifyJournalClosed() => Instance?.OnJournalClosed();
        public static void NotifyWheelReleased() => Instance?.OnWheelReleased();
        public static void NotifyCampfireUiClosed() => Instance?.OnCampfireUiClosed();
        public static void NotifyRodPurchased() => Instance?.RequestWheel();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            Build();
            Hide();
        }

        private void OnEnable() => CreatureAggro.Began += OnAggro;
        private void OnDisable() => CreatureAggro.Began -= OnAggro;

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void Update()
        {
            if (IsMainMenu()) return;
            PollFish();
            PollQuest();
            PollCampfire();
            if (DialogueIsPlaying() && _shown != null)
                ShelveShown();
            PollDismissEdges();
            TryShow();
        }

        private static bool IsMainMenu()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            return scene.IsValid() &&
                   string.Equals(scene.name, "MainMenu", System.StringComparison.OrdinalIgnoreCase);
        }

        private void PollFish()
        {
            var inv = InventoryController.Instance;
            var count = 0;
            if (inv != null)
                count = inv.GetFishItems().Count;

            if (!_fishBaseline)
            {
                _fishBaseline = true;
                _fishCount = count;
                return;
            }

            if (count > _fishCount)
                _fishWaiting = true;
            _fishCount = count;

            if (!_fishWaiting || CaughtFishPresenter.IsBusy) return;
            _fishWaiting = false;
            Request("inventory",
                "Click an item, then hold and drag it over an inventory slot to place it.\nPress I to open or close the inventory.\nWhile holding an object, press R to rotate it.",
                TipDismiss.InventoryClose,
                pause: false);
        }

        private void PollQuest()
        {
            if (_journalRequested || Seen("journal")) return;
            var manager = QuestManager.Resolve();
            if (manager == null || !manager.TryGetQuest(QuestIds.CollectPondItem, out var quest)) return;
            if (quest.state != QuestState.IN_PROGRESS && quest.state != QuestState.CAN_FINISH && quest.state != QuestState.FINISHED)
                return;
            _journalRequested = true;
            Request("journal",
                "In the Bayou you will come across multiple people who will give you quests.\nYou can track all these different quests in the journal.\nPress J to open and close the Journal.",
                TipDismiss.JournalCycle,
                pause: false);
        }

        private void PollCampfire()
        {
            if (_campfireRequested || Seen("campfire")) return;
            var player = Bayou.Player.PlayerLocator.Transform;
            if (player == null) return;
            if (Time.unscaledTime >= _nextCampfireScan)
            {
                _nextCampfireScan = Time.unscaledTime + 0.5f;
                _fires = Object.FindObjectsByType<BonfireInteractable>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            }

            for (var i = 0; i < _fires.Length; i++)
            {
                var fire = _fires[i];
                if (fire == null) continue;
                var d = fire.transform.position - player.position;
                d.y = 0f;
                if (d.sqrMagnitude > 6f * 6f) continue;
                if (!IsTutorialCampfire(fire)) continue;
                _campfireRequested = true;
                Request("campfire",
                    "Use the campfire to save your game and set your respawn point.\nIf you choose a fish to cook you will additionally restore health.",
                    TipDismiss.CampfireUi,
                    pause: false);
                return;
            }
        }

        private void OnAggro()
        {
            if (Seen("combat") || (_shown != null && _shown.Id == "combat")) return;
            Request("combat",
                "Throughout the bayou you will encounter different critters. Some of which will be hostile.\nWhen a creature becomes aggressive towards you, your net will visually show its attack arc.\nUse the left mouse button to swing your net and attack aggressive critters.",
                TipDismiss.Button,
                pause: true);
        }

        private void RequestShop()
        {
            Request("shop",
                "You can buy and sell with Caliste.\nDrag a deal together, then leave her shop when you are done.",
                TipDismiss.ShopClose,
                pause: false);
        }

        private void RequestWheel()
        {
            Request("wheel",
                "You will find multiple key items during your journey such as the rod and net.\nEither press numbers 1-4 to switch slots or hold Tab to bring up the item wheel.",
                TipDismiss.WheelRelease,
                pause: false);
        }

        private void Request(string id, string body, TipDismiss dismiss, bool pause)
        {
            if (Seen(id)) return;
            if (_shown != null && _shown.Id == id) return;
            for (var i = 0; i < _queue.Count; i++)
            {
                if (_queue[i].Id == id) return;
            }

            _queue.Add(new Tip { Id = id, Body = body, Dismiss = dismiss, Pause = pause });
            TryShow();
        }

        private void TryShow()
        {
            if (_shown != null || _queue.Count == 0) return;
            var dialogue = DialogueManager.GetInstance();
            if (dialogue != null && dialogue.dialogueIsPlaying) return;

            _shown = _queue[0];
            _queue.RemoveAt(0);
            _inventoryWasOpen = InventoryOpen();
            _shopWasOpen = ShopOpen();
            _journalWasOpen = false;
            _campfireUiWasOpen = CampfireUiOpen();
            if (_body != null)
                _body.text = _shown.Body;
            if (_panel != null)
                _panel.SetActive(true);
            if (_shown.Pause)
                GameplayPause.SetTutorialHold(true);
        }

        private void PollDismissEdges()
        {
            if (_shown == null) return;
            switch (_shown.Dismiss)
            {
                case TipDismiss.InventoryClose:
                    if (InventoryOpen()) _inventoryWasOpen = true;
                    else if (_inventoryWasOpen) Dismiss();
                    break;
                case TipDismiss.ShopClose:
                    if (ShopOpen()) _shopWasOpen = true;
                    else if (_shopWasOpen) Dismiss();
                    break;
                case TipDismiss.CampfireUi:
                    if (CampfireUiOpen()) _campfireUiWasOpen = true;
                    else if (_campfireUiWasOpen) Dismiss();
                    break;
            }
        }

        private void OnInventoryClosed()
        {
            if (_shown == null || _shown.Dismiss != TipDismiss.InventoryClose) return;
            if (_inventoryWasOpen) Dismiss();
        }

        private void OnShopClosed()
        {
            if (_shown == null || _shown.Dismiss != TipDismiss.ShopClose) return;
            Dismiss();
        }

        private void OnJournalOpened()
        {
            if (_shown != null && _shown.Dismiss == TipDismiss.JournalCycle)
                _journalWasOpen = true;
        }

        private void OnJournalClosed()
        {
            if (_shown == null || _shown.Dismiss != TipDismiss.JournalCycle) return;
            if (_journalWasOpen) Dismiss();
        }

        private void OnWheelReleased()
        {
            if (_shown == null || _shown.Dismiss != TipDismiss.WheelRelease) return;
            Dismiss();
        }

        private void OnCampfireUiClosed()
        {
            if (_shown == null || _shown.Dismiss != TipDismiss.CampfireUi) return;
            Dismiss();
        }

        private void ShelveShown()
        {
            if (_shown == null) return;
            var tip = _shown;
            _shown = null;
            Hide();
            if (tip.Pause)
                GameplayPause.SetTutorialHold(false);
            _queue.Insert(0, tip);
        }

        private static bool DialogueIsPlaying()
        {
            var dialogue = DialogueManager.GetInstance();
            return dialogue != null && dialogue.dialogueIsPlaying;
        }

        private bool IsTutorialCampfire(BonfireInteractable fire)
        {
            if (fire == null) return false;
            var id = fire.BonfireId;
            if (!string.IsNullOrEmpty(id) &&
                id.IndexOf("tutorial", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            if (!_tutorialPondKnown)
            {
                _tutorialPond = new Vector3(16.6f, 0f, -57.8f);
                var spots = Object.FindObjectsByType<Bayou.Fishing.FishingSpot>(
                    FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                for (var i = 0; i < spots.Length; i++)
                {
                    var spot = spots[i];
                    if (spot == null) continue;
                    var spotName = spot.SpotName ?? "";
                    if (spot.name.IndexOf("Tutorial", System.StringComparison.OrdinalIgnoreCase) < 0 &&
                        spotName.IndexOf("Tutorial", System.StringComparison.OrdinalIgnoreCase) < 0)
                        continue;
                    _tutorialPond = spot.transform.position;
                    break;
                }

                _tutorialPondKnown = true;
            }

            var delta = fire.transform.position - _tutorialPond;
            delta.y = 0f;
            return delta.sqrMagnitude <= 22f * 22f;
        }

        private void Dismiss()
        {
            if (_shown == null) return;
            var paused = _shown.Pause;
            MarkSeen(_shown.Id);
            _shown = null;
            Hide();
            var nextPauses = false;
            for (var i = 0; i < _queue.Count; i++)
            {
                if (_queue[i].Pause) nextPauses = true;
            }

            if (paused && !nextPauses)
                GameplayPause.SetTutorialHold(false);
            TryShow();
        }

        private void Hide()
        {
            if (_panel != null)
                _panel.SetActive(false);
        }

        private static bool Seen(string id) => PlayerPrefs.GetInt(Prefix + id, 0) != 0;

        private static void MarkSeen(string id)
        {
            PlayerPrefs.SetInt(Prefix + id, 1);
            PlayerPrefs.Save();
        }

        private static bool InventoryOpen() =>
            InventoryDisplayUI.Active != null && InventoryDisplayUI.Active.IsOpen;

        private static bool ShopOpen() =>
            ShopUIController.ActiveShop != null && ShopUIController.ActiveShop.IsOpen;

        private static bool CampfireUiOpen() =>
            BonfireUIController.Active != null && BonfireUIController.Active.IsOpen;

        private void Build()
        {
            var canvasGo = new GameObject("TutorialTipCanvas");
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 800;
            _canvas.overrideSorting = true;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            _panel = new GameObject("TipPanel", typeof(RectTransform), typeof(Image));
            _panel.transform.SetParent(canvasGo.transform, false);
            var rt = _panel.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(760f, 340f);
            var img = _panel.GetComponent<Image>();
            img.color = new Color(0.08f, 0.07f, 0.05f, 0.94f);
            img.raycastTarget = true;

            _body = CreateText("Body", _panel.transform, 24f);
            var bodyRt = _body.rectTransform;
            bodyRt.anchorMin = Vector2.zero;
            bodyRt.anchorMax = Vector2.one;
            bodyRt.offsetMin = new Vector2(36f, 88f);
            bodyRt.offsetMax = new Vector2(-36f, -28f);

            var buttonGo = new GameObject("GotIt", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonGo.transform.SetParent(_panel.transform, false);
            var buttonRt = buttonGo.GetComponent<RectTransform>();
            buttonRt.anchorMin = buttonRt.anchorMax = new Vector2(0.5f, 0f);
            buttonRt.pivot = new Vector2(0.5f, 0f);
            buttonRt.anchoredPosition = new Vector2(0f, 24f);
            buttonRt.sizeDelta = new Vector2(180f, 48f);
            buttonGo.GetComponent<Image>().color = new Color(0.45f, 0.32f, 0.14f, 1f);
            var button = buttonGo.GetComponent<Button>();
            button.onClick.AddListener(Dismiss);
            var label = CreateText("Label", buttonGo.transform, 22f);
            label.text = "Got it";
            label.alignment = TextAlignmentOptions.Center;
            label.fontStyle = FontStyles.Bold;
            var labelRt = label.rectTransform;
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;
        }

        private static TextMeshProUGUI CreateText(string name, Transform parent, float size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = size;
            tmp.color = new Color(0.95f, 0.92f, 0.84f, 1f);
            tmp.alignment = TextAlignmentOptions.TopLeft;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.raycastTarget = false;
            var font = TMP_Settings.defaultFontAsset;
            if (font == null)
                font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (font != null)
                tmp.font = font;
            return tmp;
        }
    }
}
