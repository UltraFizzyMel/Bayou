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
        [Tooltip("The main backpack UI shown as the left 'Personal' panel while the shop is open. Auto-resolved if left empty.")]
        [SerializeField] private InventoryUIController playerInventoryUi;
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

            // The visible "Personal" panel is the main backpack UI (InventoryUIController); the shop's own
            // playerPanel is hidden by the split layout. Bridge dragging between the two visible grids.
            playerPanel.SetCrossPanelDropHandler(TryDropPersonalToMerchant);
            merchantPanel.SetCrossPanelDropHandler(TryDropMerchantToPersonal);
            playerInventoryUi?.SetCrossPanelDropHandler(TryDropPersonalToMerchant);

            if (merchantNameLabel != null)
                merchantNameLabel.text = shopDefinition.merchantName;

            _isOpen = true;
            ActiveShop = this;

            GameplayPause.SyncFromUiState();
            ApplySplitLayout(true);
            playerInventoryUi?.Refresh();

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
            playerInventoryUi?.SetCrossPanelDropHandler(null);

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

            playerInventoryUi?.SetCrossPanelDropHandler(null);

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

        // Personal (player backpack) -> Shopkeeper (merchant stock): mark item for selling.
        private bool TryDropPersonalToMerchant(InventoryItemView view, PointerEventData eventData)
        {
            if (view?.Item?.definition == null || merchantPanel == null ||
                _merchantBag == null || _playerInventory?.Bag == null)
                return false;

            if (!merchantPanel.ContainsScreenPoint(eventData.position, eventData.pressEventCamera))
                return false;

            if (!merchantPanel.ScreenPointToGrid(eventData.position, eventData.pressEventCamera,
                    out var compartmentId, out var hoverX, out var hoverY))
                return false;

            var item = view.Item;
            var grabOffset = playerInventoryUi?.CurrentDragGrabOffset ?? Vector2Int.zero;
            ResolveAnchor(_merchantBag, item, compartmentId, hoverX, hoverY, grabOffset, out var gx, out var gy);
            if (!_merchantBag.CanPlace(item, compartmentId, gx, gy, item.rotation))
                return false;

            var sourceBag = _playerInventory.Bag;
            sourceBag.Remove(item);
            if (!_merchantBag.TryPlace(item, compartmentId, gx, gy, item.rotation))
            {
                sourceBag.HoldItem(item);
                return false;
            }

            RefreshAfterCrossPanelMove();
            return true;
        }

        // Shopkeeper (merchant stock) -> Personal (player backpack): mark item for buying.
        private bool TryDropMerchantToPersonal(InventoryItemView view, PointerEventData eventData)
        {
            if (view?.Item?.definition == null ||
                _merchantBag == null || _playerInventory?.Bag == null)
                return false;

            var targetUi = FindPlayerInventoryUiUnderPoint(eventData.position, eventData.pressEventCamera);
            if (targetUi == null)
                return false;

            if (!targetUi.ScreenPointToGrid(eventData.position, eventData.pressEventCamera,
                    out var compartmentId, out var hoverX, out var hoverY))
                return false;

            if (!_playerInventory.IsCompartmentUnlocked(compartmentId))
                return false;

            var item = view.Item;
            var playerBag = _playerInventory.Bag;
            var grabOffset = merchantPanel?.CurrentDragGrabOffset ?? Vector2Int.zero;
            ResolveAnchor(playerBag, item, compartmentId, hoverX, hoverY, grabOffset, out var gx, out var gy);
            if (!playerBag.CanPlace(item, compartmentId, gx, gy, item.rotation))
                return false;

            _merchantBag.Remove(item);
            if (!targetUi.Inventory.TryPlace(item, compartmentId, gx, gy, item.rotation))
            {
                _merchantBag.HoldItem(item);
                return false;
            }

            RefreshAfterCrossPanelMove();
            return true;
        }

        private InventoryUIController FindPlayerInventoryUiUnderPoint(Vector2 screen, Camera cam)
        {
            var ui = FindPlayerInventoryUiViaRaycast(screen, cam);
            if (ui != null)
                return ui;

            if (playerInventoryUi != null && playerInventoryUi.ContainsScreenPoint(screen, cam))
                return playerInventoryUi;

            foreach (var candidate in UnityEngine.Object.FindObjectsByType<InventoryUIController>(FindObjectsSortMode.None))
            {
                if (candidate == null || !candidate.ContainsScreenPoint(screen, cam))
                    continue;

                playerInventoryUi = candidate;
                return candidate;
            }

            playerInventoryUi = FindFirstObjectByType<InventoryUIController>();
            return playerInventoryUi?.ContainsScreenPoint(screen, cam) == true ? playerInventoryUi : null;
        }

        private InventoryUIController FindPlayerInventoryUiViaRaycast(Vector2 screen, Camera cam)
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null)
                return null;

            var pointerData = new PointerEventData(eventSystem)
            {
                position = screen
            };

            var hits = new System.Collections.Generic.List<RaycastResult>();
            eventSystem.RaycastAll(pointerData, hits);
            Debug.Log($"[Shop Raycast] EventSystem hits={hits.Count} cam={(cam!=null?cam.name:"null")} ");
            foreach (var h in hits)
            {
                Debug.Log($"[Shop Raycast] hit: {h.gameObject.name} module={h.module?.GetType().Name}");
            }

            var ui = GetFirstInventoryUiFromHits(hits);
            if (ui != null)
                return ui;

            // If we didn't find a candidate via EventSystem, try per-canvas GraphicRaycaster checks
            var canvases = UnityEngine.Object.FindObjectsOfType<Canvas>();
            foreach (var canvas in canvases)
            {
                if (canvas == null) continue;
                var gr = canvas.GetComponent<UnityEngine.UI.GraphicRaycaster>();
                if (gr == null) continue;

                var list = new System.Collections.Generic.List<RaycastResult>();
                gr.Raycast(pointerData, list);
                if (list.Count > 0)
                {
                    Debug.Log($"[Shop Raycast] Canvas '{canvas.name}' (camera={(canvas.worldCamera!=null?canvas.worldCamera.name:"null")}) hits={list.Count}");
                    foreach (var r in list)
                        Debug.Log($"[Shop Raycast]  canvas hit: {r.gameObject.name} module={r.module?.GetType().Name}");

                    var found = GetFirstInventoryUiFromHits(list);
                    if (found != null)
                        return found;
                }
            }

            Debug.Log("[Shop Raycast] No inventory UI found under point.");
            return null;
        }

        private InventoryUIController GetFirstInventoryUiFromHits(System.Collections.Generic.List<RaycastResult> hits)
        {
            foreach (var hit in hits)
            {
                var ui = GetInventoryUiForGameObject(hit.gameObject);
                if (ui != null)
                    return ui;
            }
            return null;
        }

        private InventoryUIController GetInventoryUiForGameObject(GameObject go)
        {
            if (go == null) return null;

            foreach (var ui in UnityEngine.Object.FindObjectsByType<InventoryUIController>(FindObjectsSortMode.None))
            {
                if (ui == null || ui.PanelRoot == null) continue;
                if (go == ui.PanelRoot.gameObject || go.transform.IsChildOf(ui.PanelRoot))
                    return ui;
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
        }
    }
}
