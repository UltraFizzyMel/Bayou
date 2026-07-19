using System.Collections;
using Bayou;
using Bayou.Inventory.Shop;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Bayou.Inventory
{
    /// <summary>
    /// Catch flow: brief fish reveal → open handmade bag → drag to place or discard.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CaughtFishPresenter : MonoBehaviour
    {
        public static CaughtFishPresenter Instance { get; private set; }

        [Header("Timing")]
        [SerializeField] private float revealSeconds = 1.6f;

        [Header("Optional overrides (auto-built if empty)")]
        [SerializeField] private Canvas canvas;
        [SerializeField] private RectTransform revealRoot;
        [SerializeField] private Image revealIcon;
        [SerializeField] private TextMeshProUGUI revealTitle;
        [SerializeField] private TextMeshProUGUI revealName;
        [SerializeField] private RectTransform allocateBar;
        [SerializeField] private TextMeshProUGUI allocateHint;
        [SerializeField] private Button discardButton;

        private InventoryItemInstance _pending;
        private bool _allocating;
        private bool _revealing;
        private Coroutine _routine;

        public static bool IsAllocating =>
            Instance != null && Instance._allocating;

        /// <summary>True during the catch reveal splash or while waiting for place/discard.</summary>
        public static bool IsBusy =>
            Instance != null && (Instance._revealing || Instance._allocating);

        public InventoryItemInstance PendingItem => _pending;

        private void Awake()
        {
            Instance = this;
            EnsureUi();
            HideAll();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void OnEnable()
        {
            var inv = InventoryController.Instance;
            if (inv != null)
                inv.InventoryChanged += OnInventoryChanged;
        }

        private void OnDisable()
        {
            var inv = InventoryController.Instance;
            if (inv != null)
                inv.InventoryChanged -= OnInventoryChanged;
        }

        /// <summary>Entry point from <see cref="Bayou.Fish.BayouFish.Catch"/>.</summary>
        public static void Present(ItemDefinition fishItem)
        {
            var host = Instance;
            if (host == null)
            {
                var go = new GameObject("CaughtFishPresenter");
                host = go.AddComponent<CaughtFishPresenter>();
            }

            host.Begin(fishItem);
        }

        public void Begin(ItemDefinition fishItem)
        {
            if (fishItem == null)
            {
                Debug.LogWarning("[Catch] No ItemDefinition for caught fish.");
                return;
            }

            EnsureUi();
            EnsureInventorySubscription();

            if (_routine != null)
                StopCoroutine(_routine);

            // Drop a previous unfinished catch (rare).
            if (_pending != null && InventoryController.Instance != null)
                InventoryController.Instance.RemoveItem(_pending);

            _pending = null;
            _allocating = false;
            _revealing = false;
            _routine = StartCoroutine(CatchFlow(fishItem));
        }

        private void EnsureInventorySubscription()
        {
            var inv = InventoryController.Instance ?? FindFirstObjectByType<InventoryController>();
            if (inv == null) return;
            inv.InventoryChanged -= OnInventoryChanged;
            inv.InventoryChanged += OnInventoryChanged;
        }

        private IEnumerator CatchFlow(ItemDefinition fishItem)
        {
            _revealing = true;
            ShowReveal(fishItem);
            GameplayPause.SyncFromUiState();

            var wait = Mathf.Max(0.35f, revealSeconds);
            var t = 0f;
            while (t < wait)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }

            HideReveal();
            _revealing = false;

            var inventory = InventoryController.Instance ?? FindFirstObjectByType<InventoryController>();
            if (inventory == null || !inventory.TryHoldNewItem(fishItem, out _pending))
            {
                Debug.LogWarning("[Catch] Could not hold fish for inventory allocation.");
                EndSession(closeInventory: false);
                yield break;
            }

            _allocating = true;

            var bag = InventoryDisplayUI.Active ?? FindFirstObjectByType<InventoryDisplayUI>();
            bag?.Open();
            bag?.Refresh();

            ShowAllocateBar(fishItem);
            GameplayPause.SyncFromUiState();
            _routine = null;
        }

        private void OnInventoryChanged()
        {
            if (!_allocating || _pending == null) return;
            if (_pending.IsPlaced)
                EndSession(closeInventory: false);
        }

        public void DiscardPending()
        {
            if (!_allocating || _pending == null) return;

            var inv = InventoryController.Instance;
            inv?.RemoveItem(_pending);
            EndSession(closeInventory: true);
        }

        private void EndSession(bool closeInventory)
        {
            _allocating = false;
            _revealing = false;
            _pending = null;
            HideAllocateBar();
            HideReveal();

            if (closeInventory)
            {
                var bag = InventoryDisplayUI.Active ?? FindFirstObjectByType<InventoryDisplayUI>();
                // Force-close even if lock flag briefly races.
                if (bag != null && bag.IsOpen)
                {
                    // Temporarily not allocating so Close() is allowed.
                    bag.Close();
                }
            }

            GameplayPause.SyncFromUiState();
        }

        private void ShowReveal(ItemDefinition fishItem)
        {
            if (revealRoot != null)
                revealRoot.gameObject.SetActive(true);
            if (allocateBar != null)
                allocateBar.gameObject.SetActive(false);

            if (revealIcon != null)
            {
                revealIcon.sprite = fishItem.icon;
                revealIcon.color = fishItem.icon != null
                    ? Color.white
                    : new Color(0.25f, 0.45f, 0.85f, 1f);
                revealIcon.preserveAspect = true;
            }

            if (revealTitle != null)
                revealTitle.text = "Caught!";
            if (revealName != null)
                revealName.text = string.IsNullOrWhiteSpace(fishItem.displayName)
                    ? "Fish"
                    : fishItem.displayName;
        }

        private void HideReveal()
        {
            if (revealRoot != null)
                revealRoot.gameObject.SetActive(false);
        }

        private void ShowAllocateBar(ItemDefinition fishItem)
        {
            if (allocateBar != null)
                allocateBar.gameObject.SetActive(true);
            if (allocateHint != null)
            {
                var name = string.IsNullOrWhiteSpace(fishItem.displayName) ? "fish" : fishItem.displayName;
                allocateHint.text = $"Drag the {name} into your case — or discard it.";
            }
        }

        private void HideAllocateBar()
        {
            if (allocateBar != null)
                allocateBar.gameObject.SetActive(false);
        }

        private void HideAll()
        {
            HideReveal();
            HideAllocateBar();
        }

        private void EnsureUi()
        {
            if (canvas != null && revealRoot != null && discardButton != null)
                return;

            if (canvas == null)
            {
                var canvasGo = new GameObject("CaughtFishCanvas");
                canvasGo.transform.SetParent(transform, false);
                canvas = canvasGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 40;
                canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                var scaler = canvasGo.GetComponent<CanvasScaler>();
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;
                canvasGo.AddComponent<GraphicRaycaster>();
            }

            if (revealRoot == null)
            {
                var overlay = CreateRect("RevealOverlay", canvas.transform);
                Stretch(overlay);
                var dim = overlay.gameObject.AddComponent<Image>();
                dim.color = ShopUiStyle.OverlayDim;
                dim.raycastTarget = true;
                revealRoot = overlay;

                var card = CreateRect("Card", overlay);
                var cardRt = card;
                cardRt.anchorMin = cardRt.anchorMax = new Vector2(0.5f, 0.5f);
                cardRt.pivot = new Vector2(0.5f, 0.5f);
                cardRt.sizeDelta = new Vector2(420f, 460f);
                var cardImg = card.gameObject.AddComponent<Image>();
                cardImg.color = ShopUiStyle.PanelBrown;

                revealTitle = CreateTmp("Title", card, "Caught!", 36f, TextAlignmentOptions.Center);
                var titleRt = revealTitle.rectTransform;
                titleRt.anchorMin = new Vector2(0f, 1f);
                titleRt.anchorMax = new Vector2(1f, 1f);
                titleRt.pivot = new Vector2(0.5f, 1f);
                titleRt.sizeDelta = new Vector2(-24f, 48f);
                titleRt.anchoredPosition = new Vector2(0f, -16f);

                var iconGo = CreateRect("Icon", card);
                iconGo.anchorMin = iconGo.anchorMax = new Vector2(0.5f, 0.5f);
                iconGo.pivot = new Vector2(0.5f, 0.5f);
                iconGo.sizeDelta = new Vector2(260f, 260f);
                iconGo.anchoredPosition = new Vector2(0f, 20f);
                revealIcon = iconGo.gameObject.AddComponent<Image>();
                revealIcon.color = ShopUiStyle.CellCream;

                revealName = CreateTmp("Name", card, "Fish", 28f, TextAlignmentOptions.Center);
                var nameRt = revealName.rectTransform;
                nameRt.anchorMin = new Vector2(0f, 0f);
                nameRt.anchorMax = new Vector2(1f, 0f);
                nameRt.pivot = new Vector2(0.5f, 0f);
                nameRt.sizeDelta = new Vector2(-24f, 40f);
                nameRt.anchoredPosition = new Vector2(0f, 28f);
            }

            if (allocateBar == null)
            {
                allocateBar = CreateRect("AllocateBar", canvas.transform);
                allocateBar.anchorMin = new Vector2(0.5f, 0f);
                allocateBar.anchorMax = new Vector2(0.5f, 0f);
                allocateBar.pivot = new Vector2(0.5f, 0f);
                allocateBar.sizeDelta = new Vector2(720f, 72f);
                allocateBar.anchoredPosition = new Vector2(0f, 24f);
                var barImg = allocateBar.gameObject.AddComponent<Image>();
                barImg.color = ShopUiStyle.HeaderFooter;

                allocateHint = CreateTmp("Hint", allocateBar, "Drag the fish into your case.", 20f, TextAlignmentOptions.MidlineLeft);
                var hintRt = allocateHint.rectTransform;
                hintRt.anchorMin = new Vector2(0f, 0f);
                hintRt.anchorMax = new Vector2(1f, 1f);
                hintRt.offsetMin = new Vector2(20f, 8f);
                hintRt.offsetMax = new Vector2(-180f, -8f);

                var discardGo = CreateRect("DiscardButton", allocateBar);
                discardGo.anchorMin = discardGo.anchorMax = new Vector2(1f, 0.5f);
                discardGo.pivot = new Vector2(1f, 0.5f);
                discardGo.sizeDelta = new Vector2(150f, 44f);
                discardGo.anchoredPosition = new Vector2(-16f, 0f);
                var discardImg = discardGo.gameObject.AddComponent<Image>();
                discardImg.color = ShopUiStyle.ButtonMuted;
                discardButton = discardGo.gameObject.AddComponent<Button>();
                discardButton.targetGraphic = discardImg;
                var discardLabel = CreateTmp("Label", discardGo, "Discard", 20f, TextAlignmentOptions.Center);
                Stretch(discardLabel.rectTransform);
            }

            discardButton.onClick.RemoveListener(DiscardPending);
            discardButton.onClick.AddListener(DiscardPending);
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

        private static TextMeshProUGUI CreateTmp(string name, Transform parent, string text, float size, TextAlignmentOptions align)
        {
            var go = CreateRect(name, parent);
            var tmp = go.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.alignment = align;
            tmp.color = ShopUiStyle.TextCream;
            tmp.raycastTarget = false;
            return tmp;
        }
    }
}
