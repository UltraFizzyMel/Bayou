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

        private void Awake()
        {
            RefreshRuntimeReferences();

            if (overlayRoot != null)
                overlayRoot.gameObject.SetActive(false);

            closeDealButton?.onClick.AddListener(OnCloseDeal);
            cancelButton?.onClick.AddListener(CloseShop);

            _transaction.DealChanged += RefreshDealSummary;
            if (_wallet != null)
                _wallet.BalanceChanged += RefreshBalance;
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

            if (cancelAction?.action != null && cancelAction.action.WasPressedThisFrame())
                CloseShop();

            if (InventoryDragInput.WasRotatePressedThisFrame(rotateItemAction))
            {
                playerPanel.TryRotateDraggedItem();
                merchantPanel.TryRotateDraggedItem();
            }
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

            if (playerPanel == null || merchantPanel == null)
            {
                Debug.LogWarning("[Shop] Shop UI panels are not wired.");
                return;
            }

            _merchantBag = shopDefinition.CreateStockBag();
            _scratchBag = InventoryBagModel.Single(1, 1, "scratch");
            _transaction.BeginSession(_playerInventory.Bag, _merchantBag);

            var layout = shopDefinition.layout ?? _playerInventory.Layout;
            var unlock = (Func<string, bool>)(id => _playerInventory.IsCompartmentUnlocked(id));

            playerPanel.Configure(_playerInventory.Bag, layout, unlock);
            merchantPanel.Configure(_merchantBag, layout, _ => true);

            playerPanel.SetCrossPanelDropHandler((view, data) => TryCrossPanelDrop(playerPanel, merchantPanel, view, data));
            merchantPanel.SetCrossPanelDropHandler((view, data) => TryCrossPanelDrop(merchantPanel, playerPanel, view, data));

            if (merchantNameLabel != null)
                merchantNameLabel.text = shopDefinition.merchantName;

            _isOpen = true;
            ActiveShop = this;

            GameplayPause.SyncFromUiState();
            ApplySplitLayout(true);

            if (overlayRoot != null)
                overlayRoot.gameObject.SetActive(true);

            RefreshBalance();
            RefreshDealSummary();
            playerPanel.Refresh();
            merchantPanel.Refresh();
        }

        public void CloseShop()
        {
            if (!_isOpen) return;

            _transaction.CaptureAllItems(_playerInventory.Bag, _merchantBag, _scratchBag);
            _transaction.RevertToSnapshot(_playerInventory.Bag, _merchantBag, _scratchBag);

            playerPanel.CancelDrag();
            merchantPanel.CancelDrag();

            _isOpen = false;
            if (ActiveShop == this)
                ActiveShop = null;
            if (overlayRoot != null)
                overlayRoot.gameObject.SetActive(false);

            ApplySplitLayout(false);
            GameplayPause.SyncFromUiState();

            _playerInventory.NotifyChanged();
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

            _isOpen = false;
            if (ActiveShop == this)
                ActiveShop = null;
            if (overlayRoot != null)
                overlayRoot.gameObject.SetActive(false);

            ApplySplitLayout(false);
            GameplayPause.SyncFromUiState();

            _merchantBag = null;
            _playerInventory.NotifyChanged();
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

        private bool TryCrossPanelDrop(
            InventoryBagPanelUI sourcePanel,
            InventoryBagPanelUI targetPanel,
            InventoryItemView view,
            PointerEventData eventData)
        {
            if (sourcePanel == null || targetPanel == null || view?.Item == null)
                return false;

            if (!targetPanel.ContainsScreenPoint(eventData.position, eventData.pressEventCamera))
                return false;

            if (!targetPanel.ScreenPointToGrid(eventData.position, eventData.pressEventCamera,
                    out var compartmentId, out var hoverX, out var hoverY))
                return false;

            var sourceBag = sourcePanel.Bag;
            var targetBag = targetPanel.Bag;
            var item = view.Item;

            InventoryDragPlacement.TryGetAnchorFromHover(
                item.definition.shape,
                item.rotation,
                hoverX,
                hoverY,
                Vector2Int.zero,
                (ax, ay) => targetBag.CanPlace(item, compartmentId, ax, ay, item.rotation),
                out var gx,
                out var gy);

            if (!targetBag.CanPlace(item, compartmentId, gx, gy, item.rotation))
                return false;

            sourceBag.Remove(item);
            if (!targetBag.TryPlace(item, compartmentId, gx, gy, item.rotation))
            {
                sourceBag.TryAddItem(item.definition, item.rotation, out _);
                return false;
            }

            _transaction.Recalculate(_playerInventory.Bag, _merchantBag);
            playerPanel.Refresh();
            merchantPanel.Refresh();
            return true;
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
        }
    }
}
