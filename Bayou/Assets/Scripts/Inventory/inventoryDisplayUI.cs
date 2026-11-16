#if !ENABLE_INPUT_SYSTEM
#error InventoryDisplayUI requires the New Input System (ENABLE_INPUT_SYSTEM).
#endif

using System;
using System.Collections.Generic;
using Bayou;
using Bayou.Inventory.Shop;
using Bayou.Inventory.UI;
using Bayou.Save;
using Bayou.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Bayou.Inventory
{
    /// <summary>
    /// Player inventory UI for handmade grids (GridLayoutGroup + Cell prefab).
    /// Handles open/close, refresh, drag host callbacks, and shop cross-panel drops.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InventoryDisplayUI : MonoBehaviour, IInteractionPromptSource
    {
        public static InventoryDisplayUI Active { get; private set; }

        [Header("References")]
        [SerializeField] private InventoryController inventory;
        [SerializeField] private InventoryGridUI gridUI;
        [SerializeField] private RectTransform panelRoot;
        private Text _bagStatus;
        [SerializeField] private RectTransform itemLayer;
        [SerializeField] private InventoryItemUI itemPrefab;

        [Header("Input")]
        [SerializeField] private InputActionReference toggleInventoryAction;
        [SerializeField] private InputActionReference rotateItemAction;

        [Header("Highlight")]
        [SerializeField] private Color hoverValidColor = new(0.2f, 0.55f, 0.28f, 0.85f);

        private readonly Dictionary<string, InventoryItemUI> _displayed = new();
        private InventoryItemUI _dragging;
        private string _dragStartCompartmentId;
        private int _dragStartX;
        private int _dragStartY;
        private int _dragStartRotation;
        private Vector2Int _dragGrabOffset;
        private Vector2 _lastDragScreen;
        private Camera _lastDragCam;
        private bool _isOpen;
        private const int HoldSlotCount = 4;
        private readonly InventoryItemInstance[] _hold = new InventoryItemInstance[HoldSlotCount];
        private readonly RectTransform[] _holdSlots = new RectTransform[HoldSlotCount];
        private RectTransform _holdColumn;
        private int _dragStartHold = -1;
        private int _relayoutFrames;
        private Vector2 _stableGridSize;
        private Func<InventoryItemUI, PointerEventData, bool> _crossPanelDropHandler;

        public bool IsOpen => _isOpen;
        public InventoryController Inventory => inventory;
        public RectTransform PanelRoot => panelRoot;
        public Vector2Int CurrentDragGrabOffset => _dragGrabOffset;
        public InventoryItemUI Dragging => _dragging;

        /// <summary>
        /// Shop session keeps the bag open (I cannot dismiss it) but fully interactive for sell drags.
        /// </summary>
        public bool IsLockedByShop =>
            ShopUIController.ActiveShop != null && ShopUIController.ActiveShop.IsOpen;

        /// <summary>Catch reveal/allocation: bag cannot be dismissed until place or discard.</summary>
        public bool IsLockedByCatch => CaughtFishPresenter.IsBusy;

        public bool IsLockedByBonfire =>
            BonfireUIController.Active != null && BonfireUIController.Active.IsOpen;

        public void SetCrossPanelDropHandler(Func<InventoryItemUI, PointerEventData, bool> handler) =>
            _crossPanelDropHandler = handler;

        private void Awake()
        {
            if (inventory == null)
                inventory = InventoryController.Instance;
            if (inventory == null)
                inventory = FindFirstObjectByType<InventoryController>();

            if (panelRoot == null)
                panelRoot = transform as RectTransform;

            if (itemLayer == null)
            {
                var layer = transform.Find("ItemLayer");
                if (layer != null)
                    itemLayer = layer as RectTransform;
            }

            EnsureItemPrefab();
            EnsureEventSystem();

            Active = this;
            _isOpen = false;
            SetPanelVisible(false);
            ApplyBackpackArt();
        }

        private void ApplyBackpackArt()
        {
            var img = panelRoot != null ? panelRoot.GetComponent<UnityEngine.UI.Image>() : GetComponent<UnityEngine.UI.Image>();
            if (img == null) return;
            var sprite = Resources.Load<Sprite>("Bayou/Ui/BackpackBackground");
            if (sprite == null) return;
            img.sprite = sprite;
            img.type = UnityEngine.UI.Image.Type.Simple;
            img.preserveAspect = false;
            img.color = Color.white;
            gridUI?.UseBackpackFrame();
        }

        private void OnEnable()
        {
            if (inventory == null)
                inventory = InventoryController.Instance;

            if (inventory != null)
                inventory.InventoryChanged += Refresh;

            toggleInventoryAction?.action?.Enable();
            rotateItemAction?.action?.Enable();
            ResolveToggleFromControlsIfNeeded();
            InteractionPromptBroker.Register(this);
        }

        private void OnDisable()
        {
            if (inventory != null)
                inventory.InventoryChanged -= Refresh;
            InteractionPromptBroker.Unregister(this);
        }

        public bool TryGetInteractionPrompt(out InteractionPrompt prompt)
        {
            prompt = default;
            return false;
        }

        private void OnDestroy()
        {
            if (Active == this)
                Active = null;
        }

        private void Start()
        {
            EnsureCanvasHealthy();
            SyncGridToModel();
            // Don't layout while closed — wait for Open() so the panel has a real size.
        }

        private void Update()
        {
            if (_isOpen && WasClosePressed())
            {
                EscapeGate.Consume();
                Close();
            }
            else if (WasTogglePressed())
            {
                Toggle();
            }

            if (_dragging != null && Mouse.current != null && !Mouse.current.leftButton.isPressed)
                FinishStuckDrag();

            if (_dragging != null && InventoryDragInput.WasRotatePressedThisFrame(rotateItemAction))
                RotateDraggedItem();
        }

        private void FinishStuckDrag()
        {
            var ui = _dragging;
            RestoreRaycasts(ui);
            var system = EventSystem.current;
            if (system == null || ui == null)
            {
                CancelDrag();
                return;
            }

            var data = new PointerEventData(system)
            {
                position = _lastDragScreen,
                pressPosition = _lastDragScreen
            };
            EndDrag(ui, data);
            RestoreRaycasts(ui);
        }

        private void LateUpdate()
        {
            UpdateBagStatus();
            if (!_isOpen || gridUI == null) return;

            var gridRect = gridUI.transform as RectTransform;
            var size = gridRect != null ? gridRect.rect.size : Vector2.zero;
            var sized = size.x >= 16f && size.y >= 16f;
            if (sized && (size - _stableGridSize).sqrMagnitude > 0.25f)
            {
                _stableGridSize = size;
                _relayoutFrames = Mathf.Max(_relayoutFrames, 2);
            }

            if (_relayoutFrames <= 0) return;

            Canvas.ForceUpdateCanvases();
            if (panelRoot != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(panelRoot);
            gridUI.ApplyFillLayout();

            size = gridRect != null ? gridRect.rect.size : Vector2.zero;
            if (size.x < 16f || size.y < 16f)
                return;

            _relayoutFrames--;
            Refresh();
        }

        public void Toggle()
        {
            if (Bayou.Environment.SignReadUi.IsOpen) return;
            if (IsLockedByShop || IsLockedByCatch || IsLockedByBonfire) return;
            if (GameplayPause.IsDialoguePlaying) return;
            if (_isOpen) Close();
            else Open();
        }

        public void Open()
        {
            if (IsLockedByBonfire || IsLockedByShop) return;
            if (GameplayPause.IsDialoguePlaying) return;
            if (inventory == null)
                inventory = InventoryController.Instance;

            EnsureCanvasHealthy();
            EnsureRaycaster();
            CaughtFishPresenter.ReleaseStaleBlock();
            _isOpen = true;
            SetPanelVisible(true);
            Bayou.UI.TutorialTips.NotifyInventoryOpened();
            EnsureHoldColumn();
            RestoreItemRaycasts();
            Canvas.ForceUpdateCanvases();
            if (panelRoot != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(panelRoot);
            SyncGridToModel();
            Refresh();
            _stableGridSize = Vector2.zero;
            _relayoutFrames = 8;
            GameplayPause.SyncFromUiState();
        }

        public void Close()
        {
            if (IsLockedByCatch) return;
            ForceClose();
        }

        /// <summary>Closes even during shop so the regular bag cannot sit behind the shop UI.</summary>
        public void ForceClose()
        {
            CancelDrag();
            ReturnHoldToGrid();
            _isOpen = false;
            _relayoutFrames = 0;
            SetPanelVisible(false);
            Bayou.UI.TutorialTips.NotifyInventoryClosed();
            gridUI?.ClearHighlights();
            GameplayPause.SyncFromUiState();
        }

        /// <summary>
        /// Hides the panel without disabling this component (so Toggle input keeps working).
        /// </summary>
        private void SetPanelVisible(bool visible)
        {
            var self = transform as RectTransform;
            if (panelRoot != null && panelRoot != self)
            {
                panelRoot.gameObject.SetActive(visible);
                return;
            }

            // Script lives on the panel root — toggle children + background, keep this GO active.
            for (var i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                if (itemPrefab != null && child == itemPrefab.transform)
                {
                    child.gameObject.SetActive(false);
                    continue;
                }

                child.gameObject.SetActive(visible);
            }

            var graphic = GetComponent<Graphic>();
            if (graphic != null)
                graphic.enabled = visible;
        }

        public void Refresh()
        {
            if (inventory?.Bag == null || gridUI == null) return;

            EnsureItemPrefab();
            if (itemPrefab == null)
            {
                Debug.LogError("[Inventory] InventoryDisplayUI.itemPrefab is missing. Assign MockUI/InventoryItem.");
                return;
            }

            // While closed, only keep the data model — don't layout zero-size rects.
            if (!_isOpen)
                return;

            gridUI.ApplyFillLayout();
            Canvas.ForceUpdateCanvases();
            var gridRect = gridUI.transform as RectTransform;
            if (gridRect == null || gridRect.rect.width < 16f || gridRect.rect.height < 16f)
                return;

            SyncGridToModel();
            gridUI.EnsureBuilt();
            gridUI.ApplyFillLayout();
            Canvas.ForceUpdateCanvases();
            if (gridUI.transform is RectTransform gridRt)
                LayoutRebuilder.ForceRebuildLayoutImmediate(gridRt);
            SyncItemLayerToGrid();

            var bagItems = inventory.Bag.AllItems;
            var keep = new HashSet<string>();
            var parent = itemLayer != null ? itemLayer : (RectTransform)transform;

            foreach (var item in bagItems)
            {
                if (item == null) continue;
                keep.Add(item.instanceId);

                if (!_displayed.TryGetValue(item.instanceId, out var ui) || ui == null)
                {
                    ui = Instantiate(itemPrefab, parent);
                    if (ui.GetComponent<InventoryDragController>() == null)
                        ui.gameObject.AddComponent<InventoryDragController>();
                    ui.gameObject.SetActive(true);
                    ui.name = item.definition != null ? item.definition.displayName : "Item";
                    ui.SetItem(item);
                    _displayed[item.instanceId] = ui;
                }

                if (_dragging != null && _dragging.Item == item)
                    continue;

                var holdIndex = HoldIndexOf(item);
                if (holdIndex >= 0)
                {
                    ui.gameObject.SetActive(true);
                    LayoutInHold(ui, holdIndex);
                    continue;
                }

                if (!item.IsPlaced)
                {
                    // Pending catch fish (or other held items) sit in a dock until dragged onto the grid.
                    if (ShouldShowHeldItem(item))
                    {
                        ui.gameObject.SetActive(true);
                        var dock = panelRoot != null ? panelRoot : parent;
                        LayoutHeldItem(ui, dock, item);
                    }
                    else
                    {
                        ui.gameObject.SetActive(false);
                    }

                    continue;
                }

                ui.gameObject.SetActive(true);
                ui.ApplyLayout(gridUI, parent, item.gridX, item.gridY, item.rotation);
            }

            var remove = new List<string>();
            foreach (var pair in _displayed)
            {
                if (!keep.Contains(pair.Key))
                    remove.Add(pair.Key);
            }

            foreach (var id in remove)
            {
                if (_displayed.TryGetValue(id, out var ui) && ui != null)
                    Destroy(ui.gameObject);
                _displayed.Remove(id);
            }
        }

        public bool ContainsScreenPoint(Vector2 screen, Camera cam)
        {
            if (panelRoot == null) return false;
            var canvas = panelRoot.GetComponentInParent<Canvas>();
            var overlayCam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
            if (RectTransformUtility.RectangleContainsScreenPoint(panelRoot, screen, overlayCam))
                return true;
            if (cam != overlayCam && RectTransformUtility.RectangleContainsScreenPoint(panelRoot, screen, cam))
                return true;
            return false;
        }

        public bool ScreenPointToGrid(Vector2 screen, Camera cam, out string compartmentId, out int gx, out int gy)
        {
            compartmentId = GetPrimaryCompartmentId();
            gx = gy = 0;
            if (gridUI == null) return false;

            var canvas = panelRoot != null ? panelRoot.GetComponentInParent<Canvas>() : null;
            var overlayCam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;

            // Prefer overlay-null camera so shop↔bag drops resolve correctly.
            if (gridUI.TryGetCellAtScreenPoint(screen, overlayCam, out var cell) ||
                (cam != overlayCam && gridUI.TryGetCellAtScreenPoint(screen, cam, out cell)))
            {
                gx = cell.X;
                gy = cell.Y;
                return true;
            }

            return false;
        }

        public void BeginDrag(InventoryItemUI ui, PointerEventData eventData)
        {
            if (ui?.Item == null || inventory == null) return;

            _dragging = ui;
            if (ui.Rect != null)
                ui.Rect.localScale = Vector3.one;
            var item = ui.Item;
            _dragStartHold = HoldIndexOf(item);
            if (_dragStartHold >= 0)
                _hold[_dragStartHold] = null;
            _dragStartCompartmentId = item.compartmentId;
            _dragStartX = item.gridX;
            _dragStartY = item.gridY;
            _dragStartRotation = item.rotation;

            if (ScreenPointToGrid(eventData.position, eventData.pressEventCamera, out _, out var hx, out var hy)
                && item.IsPlaced)
            {
                _dragGrabOffset = InventoryDragPlacement.ComputeGrabOffset(
                    item.definition.shape, item.rotation, item.gridX, item.gridY, hx, hy);
            }
            else
            {
                _dragGrabOffset = Vector2Int.zero;
            }

            inventory.DetachForDrag(item);

            SyncItemLayerToGrid();
            InventoryDragOverlay.Attach(ui.Rect);
            ui.transform.SetAsLastSibling();
            _lastDragScreen = eventData.position;
            _lastDragCam = eventData.pressEventCamera;
        }

        public void Drag(InventoryItemUI ui, PointerEventData eventData)
        {
            if (ui == null || ui != _dragging) return;

            _lastDragScreen = eventData.position;
            _lastDragCam = eventData.pressEventCamera;

            if (!TrySnapDragToGrid(ui, eventData.position, eventData.pressEventCamera))
                InventoryDragOverlay.Follow(ui.Rect, eventData);

            UpdateHoverPreview(eventData.position, eventData.pressEventCamera);
        }

        public void EndDrag(InventoryItemUI ui, PointerEventData eventData)
        {
            RestoreRaycasts(ui);
            if (ui == null || ui != _dragging || inventory?.Bag == null)
            {
                _dragging = null;
                _dragStartHold = -1;
                gridUI?.ClearHighlights();
                return;
            }

            var item = ui.Item;
            gridUI?.ClearHighlights();

            if (TryHoldSlotAt(eventData.position, eventData.pressEventCamera, out var holdSlot) ||
                TryHoldSlotAt(eventData.position, null, out holdSlot))
            {
                ParkInHold(item, holdSlot);
                _dragging = null;
                _dragStartHold = -1;
                Refresh();
                return;
            }

            var overOwnGrid = ScreenPointToGrid(eventData.position, eventData.pressEventCamera,
                                   out var compartmentId, out var hoverX, out var hoverY) ||
                               ScreenPointToGrid(eventData.position, null,
                                   out compartmentId, out hoverX, out hoverY);

            if (overOwnGrid)
            {
                InventoryDragPlacement.TryGetAnchorFromHover(
                    item.definition.shape,
                    item.rotation,
                    hoverX,
                    hoverY,
                    _dragGrabOffset,
                    (ax, ay) => inventory.Bag.CanPlace(item, compartmentId, ax, ay, item.rotation),
                    out var gx,
                    out var gy);

                if (inventory.TryPlace(item, compartmentId, gx, gy, item.rotation))
                {
                    _dragging = null;
                    Refresh();
                    return;
                }
            }

            if (_crossPanelDropHandler != null && _crossPanelDropHandler(ui, eventData))
            {
                _dragging = null;
                Refresh();
                return;
            }

            var openShop = ShopUIController.ActiveShop;
            if (openShop != null && openShop.IsOpen &&
                openShop.TryPlaceIntoOpenPlayerBag(item, eventData.position))
            {
                _dragging = null;
                Refresh();
                return;
            }

            // Dropping the accordion off the bag discards it, unless Olivier still needs it.
            if (item != null && Bayou.World.AccordionCustody.IsAccordion(item) &&
                !Bayou.World.AccordionCustody.WasBlockedThisFrame)
            {
                if (Bayou.World.AccordionCustody.LostQuestInProgress())
                    Bayou.World.AccordionCustody.ShowBlocked();
                else
                {
                    inventory.RemoveItem(item);
                    _dragging = null;
                    Refresh();
                    return;
                }
            }

            // Revert.
            if (_dragStartHold >= 0)
            {
                _hold[_dragStartHold] = item;
                inventory.Bag.HoldItem(item);
            }
            else if (!string.IsNullOrEmpty(_dragStartCompartmentId))
            {
                inventory.TryPlace(item, _dragStartCompartmentId, _dragStartX, _dragStartY, _dragStartRotation);
            }
            else
            {
                inventory.Bag.HoldItem(item);
            }

            _dragging = null;
            _dragStartHold = -1;
            Refresh();
        }

        public void CancelDrag()
        {
            var ui = _dragging;
            RestoreRaycasts(ui);
            if (ui?.Item == null || inventory == null)
            {
                _dragging = null;
                _dragStartHold = -1;
                return;
            }

            var item = ui.Item;
            if (_dragStartHold >= 0)
            {
                _hold[_dragStartHold] = item;
                inventory.Bag?.HoldItem(item);
            }
            else if (!string.IsNullOrEmpty(_dragStartCompartmentId))
            {
                inventory.TryPlace(item, _dragStartCompartmentId, _dragStartX, _dragStartY, _dragStartRotation);
            }
            else
            {
                inventory.Bag?.HoldItem(item);
            }

            _dragging = null;
            _dragStartHold = -1;
            gridUI?.ClearHighlights();
            Refresh();
        }

        private bool ShouldShowHeldItem(InventoryItemInstance item)
        {
            if (item == null) return false;
            // Unique gear and any other unplaced bag item stay visible after pickup.
            if (item.definition != null && item.definition.IsUniqueEquipment)
                return true;
            if (!CaughtFishPresenter.IsAllocating) return false;
            var pending = CaughtFishPresenter.Instance != null
                ? CaughtFishPresenter.Instance.PendingItem
                : null;
            return pending != null && pending.instanceId == item.instanceId;
        }

        private void LayoutHeldItem(InventoryItemUI ui, RectTransform parent, InventoryItemInstance item)
        {
            if (ui == null || parent == null || item?.definition == null || gridUI == null)
                return;

            if (ui.transform.parent != parent)
                ui.transform.SetParent(parent, false);

            ui.ApplySize(gridUI, item.rotation);

            var rt = ui.Rect;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);

            var heldIndex = 0;
            var heldCount = 0;
            var bagItems = inventory.Bag.AllItems;
            for (var i = 0; i < bagItems.Count; i++)
            {
                var other = bagItems[i];
                if (other == null || other.IsPlaced || !ShouldShowHeldItem(other))
                    continue;
                if (other == item)
                    heldIndex = heldCount;
                heldCount++;
            }

            var spacing = 96f;
            var x = heldCount <= 1 ? 0f : (heldIndex - (heldCount - 1) * 0.5f) * spacing;
            rt.anchoredPosition = new Vector2(x, 96f);
            ui.transform.SetAsLastSibling();
        }

        public void RotateDraggedItem()
        {
            if (_dragging?.Item == null) return;
            var oldRot = _dragging.Item.rotation;
            _dragging.Item.rotation = (oldRot + 1) % 4;
            if (_dragging.Item.definition != null)
                _dragGrabOffset = InventoryDragPlacement.RotateGrabClockwise(
                    _dragging.Item.definition.shape, oldRot, _dragGrabOffset);
            _dragging.ApplySize(gridUI, _dragging.Item.rotation);
            TrySnapDragToGrid(_dragging, _lastDragScreen, _lastDragCam);
            UpdateHoverPreview(_lastDragScreen, _lastDragCam);
        }

        private bool TrySnapDragToGrid(InventoryItemUI ui, Vector2 screen, Camera cam)
        {
            if (ui?.Item?.definition == null || gridUI == null || inventory?.Bag == null || itemLayer == null)
                return false;

            if (!ScreenPointToGrid(screen, cam, out var compartmentId, out var hoverX, out var hoverY) &&
                !ScreenPointToGrid(screen, null, out compartmentId, out hoverX, out hoverY))
                return false;

            var item = ui.Item;
            InventoryDragPlacement.TryGetAnchorFromHover(
                item.definition.shape,
                item.rotation,
                hoverX,
                hoverY,
                _dragGrabOffset,
                (ax, ay) => inventory.Bag.CanPlace(item, compartmentId, ax, ay, item.rotation),
                out var gx,
                out var gy);

            InventoryDragOverlay.SetMaskable(ui.Rect, false);
            if (ui.Rect.parent != itemLayer)
                ui.Rect.SetParent(itemLayer, false);

            ui.ApplyLayout(gridUI, itemLayer, gx, gy, item.rotation);
            ui.transform.SetAsLastSibling();
            return true;
        }

        private void UpdateHoverPreview(Vector2 screen, Camera cam)
        {
            if (_dragging?.Item?.definition == null || gridUI == null || inventory?.Bag == null)
                return;

            if (!ScreenPointToGrid(screen, cam, out var compartmentId, out var hoverX, out var hoverY) &&
                !ScreenPointToGrid(screen, null, out compartmentId, out hoverX, out hoverY))
            {
                gridUI.ClearHighlights();
                return;
            }

            var item = _dragging.Item;
            InventoryDragPlacement.TryGetAnchorFromHover(
                item.definition.shape,
                item.rotation,
                hoverX,
                hoverY,
                _dragGrabOffset,
                (ax, ay) => inventory.Bag.CanPlace(item, compartmentId, ax, ay, item.rotation),
                out var gx,
                out var gy);

            var valid = inventory.Bag.CanPlace(item, compartmentId, gx, gy, item.rotation);
            gridUI.HighlightShape(gx, gy, item.definition.shape, item.rotation, hoverValidColor, valid);
        }

        private void SyncGridToModel()
        {
            if (inventory?.Grid == null || gridUI == null) return;
            if (gridUI.Columns != inventory.Grid.Width || gridUI.Rows != inventory.Grid.Height)
                gridUI.ConfigureSize(inventory.Grid.Width, inventory.Grid.Height);
            else
                gridUI.EnsureBuilt();
        }

        private string GetPrimaryCompartmentId()
        {
            if (inventory?.Bag != null && inventory.Bag.CompartmentIds.Count > 0)
                return inventory.Bag.CompartmentIds[0];
            return "main";
        }

        private void ResolveToggleFromControlsIfNeeded()
        {
            if (toggleInventoryAction != null) return;
            // Will still work via Keyboard I fallback in WasTogglePressed.
        }

        /// <summary>
        /// ItemLayer covers the same area as the grid, with top-left pivot so index math matches GridLayoutGroup.
        /// </summary>
        private void SyncItemLayerToGrid()
        {
            if (gridUI == null) return;

            var gridRect = gridUI.transform as RectTransform;
            if (gridRect == null) return;

            if (itemLayer == null)
            {
                var existing = transform.Find("ItemLayer") as RectTransform;
                if (existing != null)
                    itemLayer = existing;
                else
                {
                    var go = new GameObject("ItemLayer", typeof(RectTransform));
                    itemLayer = go.GetComponent<RectTransform>();
                }
            }

            if (itemLayer.parent != gridRect.parent)
                itemLayer.SetParent(gridRect.parent, false);

            // Identical rect + top-left pivot as the grid (sibling, not a GridLayout child).
            itemLayer.anchorMin = gridRect.anchorMin;
            itemLayer.anchorMax = gridRect.anchorMax;
            itemLayer.pivot = new Vector2(0f, 1f);
            itemLayer.offsetMin = gridRect.offsetMin;
            itemLayer.offsetMax = gridRect.offsetMax;
            itemLayer.localRotation = Quaternion.identity;
            itemLayer.localScale = Vector3.one;
            itemLayer.SetAsLastSibling();
            var layerLayout = itemLayer.GetComponent<LayoutElement>();
            if (layerLayout == null)
                layerLayout = itemLayer.gameObject.AddComponent<LayoutElement>();
            layerLayout.ignoreLayout = true;

            // ItemLayer must never run a layout group — that would crush multi-cell items to 1×1.
            var layoutGroup = itemLayer.GetComponent<LayoutGroup>();
            if (layoutGroup != null)
                Destroy(layoutGroup);
        }

        private void EnsureHoldColumn()
        {
            if (_holdColumn != null) return;
            var parent = panelRoot != null ? panelRoot : transform as RectTransform;
            if (parent == null) return;

            var column = new GameObject("HoldColumn", typeof(RectTransform));
            column.transform.SetParent(parent, false);
            _holdColumn = column.GetComponent<RectTransform>();
            _holdColumn.anchorMin = _holdColumn.anchorMax = new Vector2(1f, 0.5f);
            _holdColumn.pivot = new Vector2(1f, 0.5f);
            _holdColumn.anchoredPosition = new Vector2(-16f, 0f);
            _holdColumn.sizeDelta = new Vector2(76f, HoldSlotCount * 78f);
            var ignore = column.AddComponent<LayoutElement>();
            ignore.ignoreLayout = true;

            for (var i = 0; i < HoldSlotCount; i++)
            {
                var slotGo = new GameObject("HoldSlot", typeof(RectTransform), typeof(Image));
                slotGo.transform.SetParent(_holdColumn, false);
                var rt = slotGo.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.sizeDelta = new Vector2(68f, 68f);
                rt.anchoredPosition = new Vector2(0f, -8f - i * 76f);
                var img = slotGo.GetComponent<Image>();
                img.sprite = UiWhiteSprite.Get();
                img.type = Image.Type.Simple;
                img.color = new Color(0.16f, 0.12f, 0.08f, 0.78f);
                img.raycastTarget = true;
                _holdSlots[i] = rt;
            }
        }

        private int HoldIndexOf(InventoryItemInstance item)
        {
            if (item == null) return -1;
            for (var i = 0; i < HoldSlotCount; i++)
            {
                if (_hold[i] == item)
                    return i;
            }

            return -1;
        }

        private bool TryHoldSlotAt(Vector2 screen, Camera cam, out int slot)
        {
            slot = -1;
            for (var i = 0; i < HoldSlotCount; i++)
            {
                var rt = _holdSlots[i];
                if (rt == null || !rt.gameObject.activeInHierarchy) continue;
                if (!RectTransformUtility.RectangleContainsScreenPoint(rt, screen, cam)) continue;
                slot = i;
                return true;
            }

            return false;
        }

        private void ParkInHold(InventoryItemInstance item, int slot)
        {
            if (item == null || inventory?.Bag == null || slot < 0 || slot >= HoldSlotCount) return;
            ClearHold(item);
            var occupant = _hold[slot];
            _hold[slot] = item;
            inventory.Bag.HoldItem(item);
            if (occupant == null || occupant == item) return;

            if (_dragStartHold >= 0 && _hold[_dragStartHold] == null)
            {
                _hold[_dragStartHold] = occupant;
                inventory.Bag.HoldItem(occupant);
                return;
            }

            if (!string.IsNullOrEmpty(_dragStartCompartmentId) &&
                inventory.TryPlace(occupant, _dragStartCompartmentId, _dragStartX, _dragStartY, occupant.rotation))
                return;

            if (inventory.TryPlaceAnywhere(occupant))
                return;

            for (var i = 0; i < HoldSlotCount; i++)
            {
                if (_hold[i] != null) continue;
                _hold[i] = occupant;
                inventory.Bag.HoldItem(occupant);
                return;
            }

            _hold[slot] = occupant;
            inventory.Bag.HoldItem(occupant);
            if (_dragStartHold >= 0)
                _hold[_dragStartHold] = item;
            else if (!string.IsNullOrEmpty(_dragStartCompartmentId))
                inventory.TryPlace(item, _dragStartCompartmentId, _dragStartX, _dragStartY, _dragStartRotation);
            else
                inventory.Bag.HoldItem(item);
        }

        private void ClearHold(InventoryItemInstance item)
        {
            if (item == null) return;
            for (var i = 0; i < HoldSlotCount; i++)
            {
                if (_hold[i] == item)
                    _hold[i] = null;
            }
        }

        private void LayoutInHold(InventoryItemUI ui, int slot)
        {
            var parent = _holdSlots[slot];
            if (ui == null || parent == null) return;
            if (ui.transform.parent != parent)
                ui.transform.SetParent(parent, false);
            if (gridUI != null && ui.Item != null)
                ui.ApplySize(gridUI, ui.Item.rotation);
            var rt = ui.Rect;
            if (rt == null) return;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            var size = rt.rect.size;
            if (size.x < 1f || size.y < 1f)
                size = rt.sizeDelta;
            var limit = 58f;
            var largest = Mathf.Max(size.x, size.y);
            rt.localScale = largest > limit ? Vector3.one * (limit / largest) : Vector3.one;
            ui.transform.SetAsLastSibling();
        }

        private void ReturnHoldToGrid()
        {
            if (inventory == null) return;
            for (var i = 0; i < HoldSlotCount; i++)
            {
                var item = _hold[i];
                if (item == null) continue;
                if (inventory.TryPlaceAnywhere(item))
                    _hold[i] = null;
                else
                    inventory.Bag?.HoldItem(item);
            }
        }

        private static void RestoreRaycasts(InventoryItemUI ui)
        {
            if (ui == null) return;
            var cg = ui.GetComponent<CanvasGroup>();
            if (cg == null) return;
            cg.blocksRaycasts = true;
            cg.alpha = 1f;
        }

        private void RestoreItemRaycasts()
        {
            foreach (var ui in _displayed.Values)
            {
                if (ui == null || ui == _dragging) continue;
                RestoreRaycasts(ui);
            }
        }

        private void EnsureRaycaster()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;
            var raycaster = canvas.GetComponent<GraphicRaycaster>();
            if (raycaster == null)
                raycaster = canvas.gameObject.AddComponent<GraphicRaycaster>();
            raycaster.enabled = true;

            var system = EventSystem.current;
            if (system == null) return;
            var module = system.GetComponent<InputSystemUIInputModule>();
            if (module != null)
                BayouUiInputBootstrap.Wire(module);
        }

        private void EnsureCanvasHealthy()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;

            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var rt = canvas.transform as RectTransform;
            if (rt != null)
            {
                // Root overlay canvas must fill the screen — zero-size roots park UI in world space.
                if (rt.localScale.sqrMagnitude < 0.0001f)
                    rt.localScale = Vector3.one;

                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                rt.localRotation = Quaternion.identity;
            }

            var scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;
            }

            // Keep the bag a centered case when not docked for shop (shop stretch-docks the panel).
            if (panelRoot != null && panelRoot != rt && !IsStretchDocked(panelRoot))
            {
                panelRoot.anchorMin = new Vector2(0.5f, 0.5f);
                panelRoot.anchorMax = new Vector2(0.5f, 0.5f);
                panelRoot.pivot = new Vector2(0.5f, 0.5f);
                if (panelRoot.sizeDelta.x < 200f || panelRoot.sizeDelta.y < 200f)
                    panelRoot.sizeDelta = new Vector2(920f, 720f);
                panelRoot.anchoredPosition = Vector2.zero;
                panelRoot.localScale = Vector3.one;
                panelRoot.localRotation = Quaternion.identity;
            }
        }

        private static bool IsStretchDocked(RectTransform panel)
        {
            if (panel == null) return false;
            var min = panel.anchorMin;
            var max = panel.anchorMax;
            return Mathf.Abs(max.x - min.x) > 0.05f || Mathf.Abs(max.y - min.y) > 0.05f;
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;

            var existing = FindFirstObjectByType<EventSystem>();
            if (existing != null) return;

            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
        }

        private void EnsureItemPrefab()
        {
            // Unity "missing" references can deserialize as a non-null fake; treat destroyed as null.
            if (itemPrefab != null)
            {
                itemPrefab.gameObject.SetActive(false);
                return;
            }

            // 1) Inactive template under this panel (common in InventoryTest).
            var local = GetComponentsInChildren<InventoryItemUI>(includeInactive: true);
            foreach (var candidate in local)
            {
                if (candidate == null) continue;
                if (_displayed.ContainsValue(candidate)) continue;
                itemPrefab = candidate;
                break;
            }

            // 2) Any scene template.
            if (itemPrefab == null)
            {
                foreach (var candidate in FindObjectsByType<InventoryItemUI>(
                             FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (candidate == null) continue;
                    if (_displayed.ContainsValue(candidate)) continue;
                    itemPrefab = candidate;
                    break;
                }
            }

            // 3) Runtime fallback so Add Fish never hard-crashes.
            if (itemPrefab == null)
                itemPrefab = CreateRuntimeItemTemplate();

            if (itemPrefab != null)
                itemPrefab.gameObject.SetActive(false);
        }

        private InventoryItemUI CreateRuntimeItemTemplate()
        {
            var go = new GameObject("InventoryItem_Template", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(transform, false);
            go.SetActive(false);

            var image = go.GetComponent<Image>();
            image.raycastTarget = true;
            image.sprite = UiWhiteSprite.Get();
            image.type = Image.Type.Simple;
            image.preserveAspect = false;
            image.color = Color.white;

            var ui = go.AddComponent<InventoryItemUI>();
            go.AddComponent<InventoryDragController>();
            return ui;
        }

        private bool WasTogglePressed()
        {
            if (toggleInventoryAction?.action != null)
            {
                if (!toggleInventoryAction.action.enabled)
                    toggleInventoryAction.action.Enable();
                if (toggleInventoryAction.action.WasPressedThisFrame())
                    return true;
            }

            var kb = Keyboard.current;
            return kb != null && kb.iKey.wasPressedThisFrame;
        }

        private void UpdateBagStatus()
        {
            if (_bagStatus == null)
                _bagStatus = CreateBagStatus();
            if (_bagStatus == null) return;

            var show = _isOpen && panelRoot != null && panelRoot.gameObject.activeInHierarchy;
            if (_bagStatus.gameObject.activeSelf != show)
                _bagStatus.gameObject.SetActive(show);
            if (!show) return;

            var corners = new Vector3[4];
            panelRoot.GetWorldCorners(corners);
            var top = (corners[1] + corners[2]) * 0.5f;
            _bagStatus.rectTransform.position = top + new Vector3(0f, -28f, 0f);

            var wallet = PlayerWallet.Instance;
            var money = wallet != null ? wallet.Balance : 0;
            _bagStatus.text = $"${money}          Esc  close    I  close";
        }

        private Text CreateBagStatus()
        {
            var canvas = panelRoot != null
                ? panelRoot.GetComponentInParent<Canvas>()
                : GetComponentInParent<Canvas>();
            if (canvas == null) return null;

            var go = new GameObject("BagStatus", typeof(RectTransform));
            go.transform.SetParent(canvas.transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(520f, 36f);
            var text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 22;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(0.96f, 0.91f, 0.78f, 1f);
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            return text;
        }

        private static bool WasClosePressed()
        {
            var kb = Keyboard.current;
            return kb != null && kb.escapeKey.wasPressedThisFrame;
        }
    }
}
