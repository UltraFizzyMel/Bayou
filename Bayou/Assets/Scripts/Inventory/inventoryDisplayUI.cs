#if !ENABLE_INPUT_SYSTEM
#error InventoryDisplayUI requires the New Input System (ENABLE_INPUT_SYSTEM).
#endif

using System;
using System.Collections.Generic;
using Bayou;
using Bayou.Inventory.Shop;
using Bayou.Inventory.UI;
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
    public sealed class InventoryDisplayUI : MonoBehaviour
    {
        public static InventoryDisplayUI Active { get; private set; }

        [Header("References")]
        [SerializeField] private InventoryController inventory;
        [SerializeField] private InventoryGridUI gridUI;
        [SerializeField] private RectTransform panelRoot;
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
        private bool _isOpen;
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
        }

        private void OnDisable()
        {
            if (inventory != null)
                inventory.InventoryChanged -= Refresh;
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
            if (WasTogglePressed())
                Toggle();

            if (_dragging != null && InventoryDragInput.WasRotatePressedThisFrame(rotateItemAction))
                RotateDraggedItem();
        }

        public void Toggle()
        {
            if (IsLockedByShop) return;
            if (_isOpen) Close();
            else Open();
        }

        public void Open()
        {
            if (inventory == null)
                inventory = InventoryController.Instance;

            EnsureCanvasHealthy();
            _isOpen = true;
            SetPanelVisible(true);
            Canvas.ForceUpdateCanvases();
            SyncGridToModel();
            Refresh();
            GameplayPause.SyncFromUiState();
        }

        public void Close()
        {
            if (IsLockedByShop) return;

            CancelDrag();
            _isOpen = false;
            SetPanelVisible(false);
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

                if (!item.IsPlaced)
                {
                    ui.gameObject.SetActive(false);
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
            var item = ui.Item;
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
            if (itemLayer != null)
                ui.transform.SetParent(itemLayer, worldPositionStays: true);

            ui.transform.SetAsLastSibling();
        }

        public void Drag(InventoryItemUI ui, PointerEventData eventData)
        {
            if (ui == null || ui != _dragging) return;

            var canvas = ui.GetComponentInParent<Canvas>();
            if (canvas == null) return;

            ui.Rect.anchoredPosition += eventData.delta / canvas.scaleFactor;
            UpdateHoverPreview(eventData);
        }

        public void EndDrag(InventoryItemUI ui, PointerEventData eventData)
        {
            if (ui == null || ui != _dragging || inventory?.Bag == null)
            {
                _dragging = null;
                gridUI?.ClearHighlights();
                return;
            }

            var item = ui.Item;
            gridUI?.ClearHighlights();

            var cg = ui.GetComponent<CanvasGroup>();
            if (cg != null) cg.blocksRaycasts = true;

            // Shop: drop onto merchant panel.
            if (_crossPanelDropHandler != null && _crossPanelDropHandler(ui, eventData))
            {
                _dragging = null;
                Refresh();
                return;
            }

            if (ScreenPointToGrid(eventData.position, eventData.pressEventCamera,
                    out var compartmentId, out var hoverX, out var hoverY))
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

            // Revert.
            if (!string.IsNullOrEmpty(_dragStartCompartmentId))
                inventory.TryPlace(item, _dragStartCompartmentId, _dragStartX, _dragStartY, _dragStartRotation);
            else
                inventory.Bag.HoldItem(item);

            _dragging = null;
            Refresh();
        }

        public void CancelDrag()
        {
            if (_dragging?.Item == null || inventory == null)
            {
                _dragging = null;
                return;
            }

            var item = _dragging.Item;
            if (!string.IsNullOrEmpty(_dragStartCompartmentId))
                inventory.TryPlace(item, _dragStartCompartmentId, _dragStartX, _dragStartY, _dragStartRotation);

            _dragging = null;
            gridUI?.ClearHighlights();
            Refresh();
        }

        public void RotateDraggedItem()
        {
            if (_dragging?.Item == null) return;
            _dragging.Item.rotation = (_dragging.Item.rotation + 1) % 4;
            _dragging.ApplySize(gridUI, _dragging.Item.rotation);
        }

        private void UpdateHoverPreview(PointerEventData eventData)
        {
            if (_dragging?.Item?.definition == null || gridUI == null || inventory?.Bag == null)
                return;

            if (!ScreenPointToGrid(eventData.position, eventData.pressEventCamera,
                    out var compartmentId, out var hoverX, out var hoverY))
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

            // ItemLayer must never run a layout group — that would crush multi-cell items to 1×1.
            var layoutGroup = itemLayer.GetComponent<LayoutGroup>();
            if (layoutGroup != null)
                Destroy(layoutGroup);
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
    }
}
