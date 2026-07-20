#if !ENABLE_INPUT_SYSTEM
#error ShopUIController requires the New Input System (ENABLE_INPUT_SYSTEM).
#endif

using System;
using Bayou.Inventory;
using Bayou.Inventory.UI;
using Bayou;
using Bayou.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

namespace Bayou.Inventory.Shop
{
    [DisallowMultipleComponent]
    public sealed class ShopUIController : MonoBehaviour
    {
        [SerializeField] private ShopDefinition shopDefinition;
        [SerializeField] private RectTransform overlayRoot;
        [SerializeField] private InventoryBagPanelUI playerPanel;
        [SerializeField] private InventoryBagPanelUI merchantPanel;
        [Tooltip("Procedural backpack UI (optional). Prefer handmade InventoryDisplayUI when present.")]
        [SerializeField] private InventoryUIController playerInventoryUi;

        [Tooltip("Handmade grid inventory (InventoryTest / MockUI). Auto-resolved if left empty.")]
        [SerializeField] private InventoryDisplayUI handmadePlayerInventoryUi;
        [SerializeField] private TextMeshProUGUI merchantNameLabel;
        [SerializeField] private TextMeshProUGUI balanceLabel;
        [SerializeField] private TextMeshProUGUI dealSummaryLabel;
        [SerializeField] private Button closeDealButton;
        [SerializeField] private Button cancelButton;
        [SerializeField] private InputActionReference cancelAction;
        [SerializeField] private InputActionReference rotateItemAction;

        private InventoryBagModel _merchantBag;
        private InventoryBagModel _scratchBag;
        private readonly ShopTransaction _transaction = new();
        private InventoryController _playerInventory;
        private PlayerWallet _wallet;
        private bool _isOpen;

        public static ShopUIController ActiveShop { get; private set; }
        public bool IsOpen => _isOpen;
        public ShopDefinition ShopDefinition => shopDefinition;
        public bool HasPanels => playerPanel != null && merchantPanel != null;

        public void AssignShopDefinition(ShopDefinition definition)
        {
            if (definition != null)
                shopDefinition = definition;
        }

        public void AssignHandmadeInventory(InventoryDisplayUI display)
        {
            handmadePlayerInventoryUi = display;
        }

        /// <summary>Called by <see cref="ShopUiBuilder"/> after constructing the MockUI shop chrome.</summary>
        public void WireBuiltUi(
            ShopDefinition definition,
            RectTransform overlay,
            InventoryBagPanelUI player,
            InventoryBagPanelUI merchant,
            TextMeshProUGUI merchantName,
            TextMeshProUGUI balance,
            TextMeshProUGUI dealSummary,
            Button closeDeal,
            Button cancel)
        {
            shopDefinition = definition;
            overlayRoot = overlay;
            playerPanel = player;
            merchantPanel = merchant;
            merchantNameLabel = merchantName;
            balanceLabel = balance;
            dealSummaryLabel = dealSummary;
            closeDealButton = closeDeal;
            cancelButton = cancel;

            closeDealButton?.onClick.RemoveListener(OnCloseDeal);
            cancelButton?.onClick.RemoveListener(CloseShop);
            closeDealButton?.onClick.AddListener(OnCloseDeal);
            cancelButton?.onClick.AddListener(CloseShop);
        }

        private void Awake()
        {
            RefreshRuntimeReferences();

            // Always start closed — shop is opened by NPC interact or playtest toggle.
            ForceOverlayClosed();

            closeDealButton?.onClick.AddListener(OnCloseDeal);
            cancelButton?.onClick.AddListener(CloseShop);

            _transaction.DealChanged += RefreshDealSummary;
            if (_wallet != null)
                _wallet.BalanceChanged += RefreshBalance;
        }

        private void Start()
        {
            // Guard against scene objects left active from a previous edit session.
            if (!_isOpen)
                ForceOverlayClosed();
        }

        private void ForceOverlayClosed()
        {
            _isOpen = false;
            if (ActiveShop == this)
                ActiveShop = null;

            if (overlayRoot != null)
            {
                overlayRoot.gameObject.SetActive(false);
                var dim = overlayRoot.GetComponent<Image>();
                if (dim != null)
                    dim.raycastTarget = false;
            }
        }

        private void OnDestroy()
        {
            _transaction.DealChanged -= RefreshDealSummary;
            if (_wallet != null)
                _wallet.BalanceChanged -= RefreshBalance;
            closeDealButton?.onClick.RemoveListener(OnCloseDeal);
            cancelButton?.onClick.RemoveListener(CloseShop);
        }

        private void OnEnable()
        {
            cancelAction?.action?.Enable();
            rotateItemAction?.action?.Enable();
        }

        private void OnDisable()
        {
            cancelAction?.action?.Disable();
            rotateItemAction?.action?.Disable();
        }

        private void Update()
        {
            if (!_isOpen) return;

            if (WasClosePressed())
                CloseShop();

            if (InventoryDragInput.WasRotatePressedThisFrame(rotateItemAction))
            {
                playerPanel.TryRotateDraggedItem();
                merchantPanel.TryRotateDraggedItem();
                handmadePlayerInventoryUi?.RotateDraggedItem();
            }
        }

        private bool WasClosePressed()
        {
            if (cancelAction?.action != null && cancelAction.action.WasPressedThisFrame())
                return true;

            var kb = Keyboard.current;
            return kb != null && kb.escapeKey.wasPressedThisFrame;
        }

        public void OpenShop(ShopDefinition definition = null)
        {
            RefreshRuntimeReferences();

            if (definition != null)
                shopDefinition = definition;

            if (shopDefinition == null)
            {
                var keeper = FindFirstObjectByType<Shopkeeper>();
                if (keeper != null)
                    shopDefinition = keeper.ShopDefinition;
            }

            if (!HasPanels)
            {
                var built = ShopUiBuilder.EnsureInScene(shopDefinition ?? definition);
                if (built != null && built != this)
                {
                    built.OpenShop(shopDefinition ?? definition);
                    return;
                }

                RefreshRuntimeReferences();
            }

            if (shopDefinition == null)
            {
                Debug.LogWarning("[Shop] Missing ShopDefinition. Assign one on ShopUIController or Shopkeeper.");
                return;
            }

            if (_playerInventory == null)
            {
                Debug.LogWarning("[Shop] No InventoryController found on the player.");
                return;
            }

            if (_playerInventory.Bag == null)
            {
                Debug.LogWarning("[Shop] Player inventory bag is not initialized.");
                return;
            }

            if (_wallet == null)
            {
                _wallet = FindFirstObjectByType<PlayerWallet>();
                if (_wallet == null)
                {
                    var host = _playerInventory != null ? _playerInventory.gameObject : gameObject;
                    _wallet = host.AddComponent<PlayerWallet>();
                }

                _wallet.BalanceChanged -= RefreshBalance;
                _wallet.BalanceChanged += RefreshBalance;
            }

            if (!HasPanels)
            {
                Debug.LogWarning("[Shop] Shop UI panels are not wired.");
                return;
            }

            _merchantBag = shopDefinition.CreateStockBag(_playerInventory);
            _scratchBag = InventoryBagModel.Single(1, 1, "scratch");
            _transaction.BeginSession(_playerInventory.Bag, _merchantBag);

            if (merchantNameLabel != null)
                merchantNameLabel.text = shopDefinition.merchantName;

            _isOpen = true;
            ActiveShop = this;

            // Show + dock first so merchant Configure can rebuild an active, sized grid.
            if (overlayRoot != null)
            {
                overlayRoot.gameObject.SetActive(true);
                // Dim is visual only — must not steal clicks from the player bag.
                var dim = overlayRoot.GetComponent<Image>();
                if (dim != null)
                    dim.raycastTarget = false;
            }

            GameplayPause.SyncFromUiState();
            ApplySplitLayout(true);

            if (handmadePlayerInventoryUi != null)
                handmadePlayerInventoryUi.Open();
            else
                playerInventoryUi?.Refresh();

            var layout = shopDefinition.layout ?? _playerInventory.Layout;
            var unlock = (Func<string, bool>)(id => _playerInventory.IsCompartmentUnlocked(id));

            playerPanel.Configure(_playerInventory.Bag, layout, unlock);
            merchantPanel.Configure(_merchantBag, layout, _ => true);

            // Personal bag = handmade InventoryDisplayUI when present, else InventoryUIController.
            playerPanel.SetCrossPanelDropHandler(TryDropPersonalToMerchant);
            merchantPanel.SetCrossPanelDropHandler(TryDropMerchantToPersonal);
            playerInventoryUi?.SetCrossPanelDropHandler(TryDropPersonalToMerchant);
            handmadePlayerInventoryUi?.SetCrossPanelDropHandler(TryDropHandmadePersonalToMerchant);

            // Rebuild after dock so grid cells get a real rect size.
            merchantPanel.ForceRebuild();
            if (playerPanel.PanelRoot != null && playerPanel.PanelRoot.gameObject.activeInHierarchy)
                playerPanel.ForceRebuild();

            RefreshBalance();
            RefreshDealSummary();
        }

        public void CloseShop()
        {
            if (!_isOpen) return;

            _transaction.CaptureAllItems(_playerInventory.Bag, _merchantBag, _scratchBag);
            _transaction.RevertToSnapshot(_playerInventory.Bag, _merchantBag, _scratchBag);

            playerPanel.CancelDrag();
            merchantPanel.CancelDrag();
            handmadePlayerInventoryUi?.CancelDrag();
            playerInventoryUi?.SetCrossPanelDropHandler(null);
            handmadePlayerInventoryUi?.SetCrossPanelDropHandler(null);

            _isOpen = false;
            if (ActiveShop == this)
                ActiveShop = null;
            if (overlayRoot != null)
                overlayRoot.gameObject.SetActive(false);

            ApplySplitLayout(false);
            GameplayPause.SyncFromUiState();

            _playerInventory.NotifyChanged();
            handmadePlayerInventoryUi?.Refresh();
        }

        private void OnCloseDeal()
        {
            if (!_isOpen) return;

            if (_wallet == null)
            {
                Debug.LogWarning("[Shop] No PlayerWallet found.");
                return;
            }

            if (!_transaction.CanCloseDeal(_wallet))
            {
                RefreshDealSummary();
                return;
            }

            if (!_transaction.TryCloseDeal(_wallet))
                return;

            playerPanel?.CancelDrag();
            merchantPanel?.CancelDrag();
            handmadePlayerInventoryUi?.CancelDrag();
            playerInventoryUi?.SetCrossPanelDropHandler(null);
            handmadePlayerInventoryUi?.SetCrossPanelDropHandler(null);

            _isOpen = false;
            if (ActiveShop == this)
                ActiveShop = null;
            if (overlayRoot != null)
                overlayRoot.gameObject.SetActive(false);

            ApplySplitLayout(false);
            GameplayPause.SyncFromUiState();

            _merchantBag = null;
            _playerInventory.NotifyChanged();
            handmadePlayerInventoryUi?.Refresh();

            // Buying Caliste's Foggy Marsh key (etc.) should unlock matching gates.
            var gates = KeyGateManager.Instance ?? FindFirstObjectByType<KeyGateManager>();
            gates?.SyncKeysFromInventory();
        }

        private void ApplySplitLayout(bool active)
        {
            var layout = GameplayUiLayout.Instance;
            if (layout == null)
                layout = FindFirstObjectByType<GameplayUiLayout>();

            layout?.ApplyShopSplitLayout(active);

            if (playerPanel?.PanelRoot != null && merchantPanel?.PanelRoot != null)
            {
                var shopPlayer = playerPanel.PanelRoot.parent as RectTransform;
                var merchant = merchantPanel.PanelRoot.parent as RectTransform;
                layout?.ApplyShopPanelAnchors(merchant, shopPlayer, hideShopPlayerPanel: active);
            }
        }

        // Handmade personal bag -> merchant stock (sell).
        private bool TryDropHandmadePersonalToMerchant(InventoryItemUI ui, PointerEventData eventData)
        {
            if (ui?.Item?.definition == null || merchantPanel == null ||
                _merchantBag == null || _playerInventory?.Bag == null)
                return false;

            var screen = eventData.position;
            // Overlay-safe: ignore pressEventCamera (often the gameplay camera).
            if (!merchantPanel.ContainsScreenPoint(screen, null))
                return false;

            var item = ui.Item;
            var grabOffset = handmadePlayerInventoryUi?.CurrentDragGrabOffset ?? Vector2Int.zero;

            string compartmentId;
            int gx, gy;
            if (merchantPanel.TryPickGrid(screen, out compartmentId, out var hoverX, out var hoverY))
            {
                ResolveAnchor(_merchantBag, item, compartmentId, hoverX, hoverY, grabOffset, out gx, out gy);
                if (!_merchantBag.CanPlace(item, compartmentId, gx, gy, item.rotation) &&
                    !_merchantBag.TryFindFirstFitAnywhere(item, out compartmentId, out gx, out gy))
                    return false;
            }
            else if (!_merchantBag.TryFindFirstFitAnywhere(item, out compartmentId, out gx, out gy))
            {
                return false;
            }

            var sourceBag = _playerInventory.Bag;
            if (item.IsPlaced)
                sourceBag.DetachFromGrid(item);
            sourceBag.Remove(item);

            if (!_merchantBag.TryPlace(item, compartmentId, gx, gy, item.rotation))
            {
                // Merchant place failed — restore into player bag.
                if (!sourceBag.TryFindFirstFitAnywhere(item, out var backId, out var bx, out var by) ||
                    !sourceBag.TryPlace(item, backId, bx, by, item.rotation))
                    sourceBag.HoldItem(item);
                return false;
            }

            RefreshAfterCrossPanelMove();
            return true;
        }

        // Personal (procedural backpack) -> Shopkeeper (merchant stock): mark item for selling.
        private bool TryDropPersonalToMerchant(InventoryItemView view, PointerEventData eventData)
        {
            if (view?.Item?.definition == null || merchantPanel == null ||
                _merchantBag == null || _playerInventory?.Bag == null)
                return false;

            var screen = eventData.position;
            if (!merchantPanel.ContainsScreenPoint(screen, null))
                return false;

            var item = view.Item;
            var grabOffset = playerInventoryUi?.CurrentDragGrabOffset ?? Vector2Int.zero;

            string compartmentId;
            int gx, gy;
            if (merchantPanel.TryPickGrid(screen, out compartmentId, out var hoverX, out var hoverY))
            {
                ResolveAnchor(_merchantBag, item, compartmentId, hoverX, hoverY, grabOffset, out gx, out gy);
                if (!_merchantBag.CanPlace(item, compartmentId, gx, gy, item.rotation) &&
                    !_merchantBag.TryFindFirstFitAnywhere(item, out compartmentId, out gx, out gy))
                    return false;
            }
            else if (!_merchantBag.TryFindFirstFitAnywhere(item, out compartmentId, out gx, out gy))
            {
                return false;
            }

            var sourceBag = _playerInventory.Bag;
            if (item.IsPlaced)
                sourceBag.DetachFromGrid(item);
            sourceBag.Remove(item);
            if (!_merchantBag.TryPlace(item, compartmentId, gx, gy, item.rotation))
            {
                if (!sourceBag.TryFindFirstFitAnywhere(item, out var backId, out var bx, out var by) ||
                    !sourceBag.TryPlace(item, backId, bx, by, item.rotation))
                    sourceBag.HoldItem(item);
                return false;
            }

            RefreshAfterCrossPanelMove();
            return true;
        }

        // Shopkeeper (merchant stock) -> Personal bag: mark item for buying.
        private bool TryDropMerchantToPersonal(InventoryItemView view, PointerEventData eventData)
        {
            if (view?.Item?.definition == null ||
                _merchantBag == null || _playerInventory?.Bag == null)
                return false;

            var item = view.Item;
            if (ShopDefinition.IsUniqueAlreadyOwned(item.definition, _playerInventory))
            {
                Debug.Log($"[Shop] Already own {item.definition.displayName}.");
                return false;
            }

            var playerBag = _playerInventory.Bag;
            var grabOffset = merchantPanel?.CurrentDragGrabOffset ?? Vector2Int.zero;

            // Prefer handmade player inventory (overlay-safe camera).
            var handmade = ResolveHandmadeUnderPoint(eventData.position, null);
            if (handmade != null)
            {
                if (!handmade.ScreenPointToGrid(eventData.position, null,
                        out var compartmentId, out var hoverX, out var hoverY))
                    return false;

                if (!_playerInventory.IsCompartmentUnlocked(compartmentId))
                    return false;

                ResolveAnchor(playerBag, item, compartmentId, hoverX, hoverY, grabOffset, out var gx, out var gy);
                if (!playerBag.CanPlace(item, compartmentId, gx, gy, item.rotation))
                    return false;

                _merchantBag.Remove(item);
                if (!_playerInventory.TryPlace(item, compartmentId, gx, gy, item.rotation))
                {
                    _merchantBag.HoldItem(item);
                    return false;
                }

                RefreshAfterCrossPanelMove();
                return true;
            }

            var targetUi = FindPlayerInventoryUiUnderPoint(eventData.position, eventData.pressEventCamera);
            if (targetUi == null)
                return false;

            if (!targetUi.ScreenPointToGrid(eventData.position, eventData.pressEventCamera,
                    out var cId, out var hx, out var hy))
                return false;

            if (!_playerInventory.IsCompartmentUnlocked(cId))
                return false;

            ResolveAnchor(playerBag, item, cId, hx, hy, grabOffset, out var ax, out var ay);
            if (!playerBag.CanPlace(item, cId, ax, ay, item.rotation))
                return false;

            _merchantBag.Remove(item);
            if (!targetUi.Inventory.TryPlace(item, cId, ax, ay, item.rotation))
            {
                _merchantBag.HoldItem(item);
                return false;
            }

            RefreshAfterCrossPanelMove();
            return true;
        }

        private InventoryDisplayUI ResolveHandmadeUnderPoint(Vector2 screen, Camera cam)
        {
            if (handmadePlayerInventoryUi == null)
                handmadePlayerInventoryUi = InventoryDisplayUI.Active ?? FindFirstObjectByType<InventoryDisplayUI>();

            if (handmadePlayerInventoryUi != null &&
                handmadePlayerInventoryUi.IsOpen &&
                handmadePlayerInventoryUi.ContainsScreenPoint(screen, cam))
                return handmadePlayerInventoryUi;

            return null;
        }

        private InventoryUIController FindPlayerInventoryUiUnderPoint(Vector2 screen, Camera cam)
        {
            if (playerInventoryUi != null && playerInventoryUi.ContainsScreenPoint(screen, cam))
                return playerInventoryUi;

            foreach (var candidate in UnityEngine.Object.FindObjectsByType<InventoryUIController>(FindObjectsSortMode.None))
            {
                if (candidate == null || !candidate.ContainsScreenPoint(screen, cam))
                    continue;

                playerInventoryUi = candidate;
                return candidate;
            }

            return null;
        }

        private static void ResolveAnchor(
            InventoryBagModel targetBag,
            InventoryItemInstance item,
            string compartmentId,
            int hoverX,
            int hoverY,
            Vector2Int grabOffset,
            out int gx,
            out int gy)
        {
            InventoryDragPlacement.TryGetAnchorFromHover(
                item.definition.shape,
                item.rotation,
                hoverX,
                hoverY,
                grabOffset,
                (ax, ay) => targetBag.CanPlace(item, compartmentId, ax, ay, item.rotation),
                out gx,
                out gy);
        }

        private void RefreshAfterCrossPanelMove()
        {
            _transaction.Recalculate(_playerInventory.Bag, _merchantBag);
            _playerInventory.NotifyChanged();
            merchantPanel.Refresh();
            playerPanel?.Refresh();
            handmadePlayerInventoryUi?.Refresh();
            RefreshDealSummary();
            RefreshBalance();
        }

        private void RefreshBalance()
        {
            if (balanceLabel == null) return;
            var money = _wallet != null ? _wallet.Balance : 0;
            balanceLabel.text = $"${money}";
        }

        private void RefreshDealSummary()
        {
            if (dealSummaryLabel == null) return;

            var buy = _transaction.TotalBuyCost;
            var sell = _transaction.TotalSellCredit;
            var net = _transaction.NetCost;

            var netText = net > 0 ? $"Pay ${net}" : net < 0 ? $"Receive ${-net}" : "Even trade";
            dealSummaryLabel.text = $"Buying: ${buy}  |  Selling: ${sell}  |  {netText}";

            if (closeDealButton != null)
                closeDealButton.interactable = _wallet == null || _transaction.CanCloseDeal(_wallet);
        }

        private void RefreshRuntimeReferences()
        {
            _playerInventory = InventoryController.Instance;
            if (_playerInventory == null)
                _playerInventory = FindFirstObjectByType<InventoryController>();

            _wallet = PlayerWallet.Instance;
            if (_wallet == null)
                _wallet = FindFirstObjectByType<PlayerWallet>();

            if (playerInventoryUi == null)
                playerInventoryUi = FindFirstObjectByType<InventoryUIController>();

            if (handmadePlayerInventoryUi == null)
                handmadePlayerInventoryUi = InventoryDisplayUI.Active ?? FindFirstObjectByType<InventoryDisplayUI>();
        }
    }
}
