#if !ENABLE_INPUT_SYSTEM
#error InventoryUIController requires the New Input System (ENABLE_INPUT_SYSTEM).
#endif

using System.Collections;
using System.Collections.Generic;
using Bayou.Inventory;
using Bayou.Inventory.Shop;
using Bayou;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
namespace Bayou.Inventory.UI
{
    [DisallowMultipleComponent]
    public sealed class InventoryUIController : MonoBehaviour, IInventoryDragHost
    {
        [SerializeField] private InventoryController inventory;
        [SerializeField] private RectTransform panelRoot;
        [SerializeField] private Image backpackBackgroundImage;

        [Header("Legacy single grid (if Backpack Layout is empty)")]
        [SerializeField] private RectTransform gridRoot;
        [SerializeField] private RectTransform itemsRoot;
        [SerializeField] private InventoryCompartmentAnchor legacyGridAnchor = InventoryCompartmentAnchor.BottomRight;
        [SerializeField] private Vector2 legacyGridOffset = new(-24f, 24f);

        [SerializeField] private GameObject cellPrefab;
        [SerializeField] private GameObject itemViewPrefab;

        [Header("Layout")]
        [SerializeField] private float cellSpacing = 2f;
        [Tooltip("If zero, cell size is auto-fit to each compartment slot area.")]
        [SerializeField] private float cellSizeOverride;
        [Tooltip("When set, resizes Panel Root to the layout asset's panelSize. Leave off if you've sized the panel in the scene.")]
        [SerializeField] private bool applyPanelSizeFromLayout;
        [Tooltip("Single-compartment layouts fill the panel; grid cells scale to fit inside padding.")]
        [SerializeField] private bool gridFillsPanel = true;
        [SerializeField] private float gridPanelPadding = 12f;
        [Tooltip("Clip items and cells to the compartment bounds.")]
        [SerializeField] private bool clipItemsToGrid = true;

        [Header("Input")]
        [SerializeField] private InputActionReference toggleInventoryAction;
        [SerializeField] private InputActionReference rotateItemAction;

        [Header("Colors")]
        [SerializeField] private Color cellEmptyColor = new(0.12f, 0.14f, 0.16f, 0.55f);
        [SerializeField] private Color cellHoverValidColor = new(0.2f, 0.45f, 0.25f, 0.85f);
        [SerializeField] private Color cellHoverInvalidColor = new(0.5f, 0.15f, 0.12f, 0.85f);

        private readonly List<InventoryCompartmentUI> _compartments = new();
        private readonly List<InventoryPocketUIView> _pockets = new();
        private readonly Dictionary<string, InventoryItemView> _views = new();
        private InventoryCompartmentUI _legacyCompartment;
        private InventoryItemView _dragging;
        private bool _dragHadPlacement;
        private string _dragStartCompartmentId;
        private int _dragStartX;
        private int _dragStartY;
        private int _dragStartRotation;
        private Vector2Int _dragGrabOffset;
        private bool _isOpen;
        private Coroutine _layoutRebuildCoroutine;
        private bool _layoutRebuildPending;
        private System.Func<InventoryItemView, PointerEventData, bool> _crossPanelDropHandler;

        public bool IsOpen => _isOpen;
        public InventoryController Inventory => inventory;
        public RectTransform PanelRoot => panelRoot;
        public Vector2Int CurrentDragGrabOffset => _dragGrabOffset;

        /// <summary>
        /// Optional handler invoked when a dragged item is released outside this panel's grids
        /// (e.g. dropped onto the shop merchant panel). Return true if the drop was consumed.
        /// </summary>
        public void SetCrossPanelDropHandler(System.Func<InventoryItemView, PointerEventData, bool> handler) =>
            _crossPanelDropHandler = handler;

        public bool ContainsScreenPoint(Vector2 screen, Camera cam)
        {
            if (panelRoot == null) return false;
            var canvas = panelRoot.GetComponentInParent<Canvas>();
            var canvasCam = canvas?.worldCamera;
            if (canvasCam != cam && RectTransformUtility.RectangleContainsScreenPoint(panelRoot, screen, canvasCam))
                return true;

            if (RectTransformUtility.RectangleContainsScreenPoint(panelRoot, screen, cam))
                return true;

            return cam != null && RectTransformUtility.RectangleContainsScreenPoint(panelRoot, screen, null);
        }

        public bool ScreenPointToGrid(Vector2 screen, Camera cam, out string compartmentId, out int gx, out int gy)
        {
            var canvas = panelRoot?.GetComponentInParent<Canvas>();
            var canvasCam = canvas?.worldCamera;
            if (canvasCam != cam && ScreenPointToCompartment(screen, canvasCam, out compartmentId, out gx, out gy))
                return true;

            if (ScreenPointToCompartment(screen, cam, out compartmentId, out gx, out gy))
                return true;

            if (cam != null && ScreenPointToCompartment(screen, null, out compartmentId, out gx, out gy))
                return true;

            return false;
        }

        public void Refresh() => RefreshAll();

        private void Awake()
        {
            if (inventory == null)
                inventory = InventoryController.Instance;

            if (panelRoot != null)
                panelRoot.gameObject.SetActive(false);

            ApplyBackpackArt();
        }

        private void OnEnable()
        {
            toggleInventoryAction?.action?.Enable();
            rotateItemAction?.action?.Enable();
            if (inventory != null)
            {
                inventory.InventoryChanged += RefreshAll;
                if (inventory.CompartmentUpgradesEnabled)
                    inventory.CompartmentUnlocked += OnCompartmentUnlocked;
            }

            if (_layoutRebuildPending)
                ScheduleLayoutRebuild();
        }

        private void OnDisable()
        {
            toggleInventoryAction?.action?.Disable();
            rotateItemAction?.action?.Disable();
            if (inventory != null)
            {
                inventory.InventoryChanged -= RefreshAll;
                if (inventory.CompartmentUpgradesEnabled)
                    inventory.CompartmentUnlocked -= OnCompartmentUnlocked;
            }
        }

        private void OnCompartmentUnlocked(string _) => RefreshPocketUnlockStates();

        private void Update()
        {
            if (toggleInventoryAction?.action != null && toggleInventoryAction.action.WasPressedThisFrame())
                Toggle();

            if (_isOpen && _dragging != null && InventoryDragInput.WasRotatePressedThisFrame(rotateItemAction))
                RotateDraggingItem();
        }

        private void RotateDraggingItem()
        {
            if (_dragging == null) return;

            _dragging.RotateClockwise();

            if (Mouse.current != null &&
                TryGetDragAnchor(Mouse.current.position.ReadValue(), null, _dragging,
                    out var compartment, out _, out var gx, out var gy))
            {
                compartment.SnapItemToGrid(_dragging.Item, gx, gy, _dragging.RectTransform);
            }

            UpdatePlacementPreview(_dragging);
        }

        public void Toggle()
        {
            if (_isOpen)
                Close();
            else
                Open();
        }

        public void Open()
        {
            _isOpen = true;
            if (panelRoot != null)
                panelRoot.gameObject.SetActive(true);
            GameplayPause.SyncFromUiState();
            ScheduleLayoutRebuild();
        }

        public void Close()
        {
            _isOpen = false;
            if (panelRoot != null)
                panelRoot.gameObject.SetActive(false);
            CancelDrag();
            GameplayPause.SyncFromUiState();
        }

        private void ApplyBackpackArt()
        {
            var layout = inventory?.Layout;
            if (layout == null || panelRoot == null) return;

            if (applyPanelSizeFromLayout)
                panelRoot.sizeDelta = layout.panelSize;
            if (backpackBackgroundImage == null) return;

            if (layout.backgroundSprite != null)
            {
                backpackBackgroundImage.sprite = layout.backgroundSprite;
                backpackBackgroundImage.preserveAspect = true;
                backpackBackgroundImage.color = Color.white;
            }
            else
            {
                backpackBackgroundImage.sprite = null;
                backpackBackgroundImage.color = new Color(0.08f, 0.09f, 0.11f, 0.94f);
            }
        }

        private void ScheduleLayoutRebuild()
        {
            if (_layoutRebuildCoroutine != null)
                StopCoroutine(_layoutRebuildCoroutine);

            if (!isActiveAndEnabled)
            {
                _layoutRebuildPending = true;
                return;
            }

            _layoutRebuildPending = false;
            _layoutRebuildCoroutine = StartCoroutine(RebuildAfterLayoutPass());
        }

        private IEnumerator RebuildAfterLayoutPass()
        {
            yield return null;
            if (panelRoot != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(panelRoot);
            Canvas.ForceUpdateCanvases();
            RebuildCompartments();
            RefreshAll();
            _layoutRebuildCoroutine = null;
        }

        private void RebuildCompartments()
        {
            ClearBuiltCompartments();
            BuildCompartments();
        }

        private void ClearBuiltCompartments()
        {
            for (var i = _pockets.Count - 1; i >= 0; i--)
            {
                if (_pockets[i] != null)
                    Destroy(_pockets[i].gameObject);
            }
            _pockets.Clear();
            _compartments.Clear();
            _legacyCompartment = null;
        }

        private void BuildCompartments()
        {
            if (inventory?.Bag == null || cellPrefab == null || panelRoot == null)
                return;

            if (_compartments.Count > 0)
                return;

            var layout = inventory.Layout;
            if (layout != null && layout.compartments != null && layout.compartments.Length > 0)
            {
                if (gridFillsPanel)
                {
                    var primary = GetPrimaryCompartmentConfig(layout);
                    if (primary != null && inventory.Bag.TryGetGrid(primary.id, out var primaryGrid))
                        BuildOneCompartment(primary, primaryGrid);
                }
                else
                {
                    foreach (var cfg in layout.compartments)
                    {
                        if (cfg == null || string.IsNullOrWhiteSpace(cfg.id)) continue;
                        if (!inventory.Bag.TryGetGrid(cfg.id, out var grid)) continue;
                        BuildOneCompartment(cfg, grid);
                    }
                }
                return;
            }

            if (gridRoot == null || itemsRoot == null) return;
            var legacyGrid = inventory.Grid;
            if (legacyGrid == null) return;

            var fillPanel = gridFillsPanel;
            var pocketRt = EnsureLegacyPocket(gridRoot, itemsRoot);
            if (fillPanel)
                ApplyPanelFillRect(pocketRt);
            else
            {
                var slotArea = legacyGridSlotArea(legacyGrid);
                ApplyCompartmentRect(pocketRt, legacyGridAnchor, legacyGridOffset, slotArea);
            }

            if (fillPanel)
            {
                StretchLayerToFillParent(gridRoot);
                StretchLayerToFillParent(itemsRoot);
            }

            var cellSize = ResolveCellSize(pocketRt, legacyGrid.Width, legacyGrid.Height, fillPanel, legacyGridSlotArea(legacyGrid));
            _legacyCompartment = new InventoryCompartmentUI("main", gridRoot, itemsRoot, legacyGrid, cellSize, cellSpacing);
            PopulateCells(_legacyCompartment, legacyGrid.Width, legacyGrid.Height, cellSize, fillPanel);
            if (!fillPanel)
                FitPocketToGrid(pocketRt, gridRoot, itemsRoot);
            if (clipItemsToGrid)
                EnsureGridMask(pocketRt);
        }

        private Vector2 legacyGridSlotArea(InventoryGridModel grid)
        {
            var w = Mathf.Max(1, grid.Width);
            var h = Mathf.Max(1, grid.Height);
            var fallbackCell = 36f;
            return new Vector2(
                w * (fallbackCell + cellSpacing) - cellSpacing,
                h * (fallbackCell + cellSpacing) - cellSpacing);
        }

        private RectTransform EnsureLegacyPocket(RectTransform gridRoot, RectTransform itemsRoot)
        {
            if (gridRoot.parent is RectTransform parentRt &&
                parentRt != panelRoot &&
                parentRt.name.StartsWith("Pocket_", System.StringComparison.Ordinal))
                return parentRt;

            var pocket = new GameObject("Pocket_Legacy", typeof(RectTransform));
            var pocketRt = pocket.GetComponent<RectTransform>();
            pocketRt.SetParent(panelRoot, false);
            gridRoot.SetParent(pocketRt, false);
            itemsRoot.SetParent(pocketRt, false);
            return pocketRt;
        }

        private static InventoryCompartmentConfig GetPrimaryCompartmentConfig(BackpackLayoutDefinition layout)
        {
            if (layout?.compartments == null) return null;
            foreach (var c in layout.compartments)
            {
                if (c != null && c.fillPanel)
                    return c;
            }
            foreach (var c in layout.compartments)
            {
                if (c != null && string.Equals(c.id, "case", System.StringComparison.OrdinalIgnoreCase))
                    return c;
            }
            foreach (var c in layout.compartments)
            {
                if (c != null && !string.IsNullOrWhiteSpace(c.id))
                    return c;
            }
            return null;
        }

        private void BuildOneCompartment(InventoryCompartmentConfig cfg, InventoryGridModel grid)
        {
            var fillPanel = ShouldFillPanel(cfg);

            var pocket = new GameObject($"Pocket_{cfg.id}", typeof(RectTransform));
            var pocketRt = pocket.GetComponent<RectTransform>();
            pocketRt.SetParent(panelRoot, false);
            if (fillPanel)
                ApplyPanelFillRect(pocketRt);
            else
                ApplyCompartmentRect(pocketRt, cfg.anchor, cfg.anchoredPosition, cfg.slotAreaSize);

            var gridGo = new GameObject("Grid", typeof(RectTransform));
            var gridRt = gridGo.GetComponent<RectTransform>();
            gridRt.SetParent(pocketRt, false);

            var itemsGo = new GameObject("Items", typeof(RectTransform));
            var itemsRt = itemsGo.GetComponent<RectTransform>();
            itemsRt.SetParent(pocketRt, false);

            if (fillPanel)
            {
                StretchLayerToFillParent(gridRt);
                StretchLayerToFillParent(itemsRt);
            }
            else
            {
                SetupGridContentRect(gridRt);
                SetupGridContentRect(itemsRt);
            }

            var cellSize = ResolveCellSize(pocketRt, cfg.gridWidth, cfg.gridHeight, fillPanel, cfg.slotAreaSize);
            var ui = new InventoryCompartmentUI(cfg.id, gridRt, itemsRt, grid, cellSize, cellSpacing);

            var pocketView = pocket.AddComponent<InventoryPocketUIView>();
            pocketView.Setup(this, cfg, gridRt, itemsRt, fillPanel);
            pocketView.SetCompartmentUI(ui);
            _pockets.Add(pocketView);

            if (inventory != null && inventory.IsCompartmentUnlocked(cfg.id))
            {
                BuildCellsForPocket(pocketView, cfg, ui, fillPanel);
                pocketView.RefreshUnlockState();
            }
            else
            {
                pocketView.RefreshUnlockState();
            }

            if (!fillPanel && inventory != null && inventory.IsCompartmentUnlocked(cfg.id))
                PositionCompartmentPocket(pocketRt, cfg, gridRt, itemsRt);

            if (clipItemsToGrid)
                EnsureGridMask(pocketRt);

            _compartments.Add(ui);
        }

        public void BuildCellsForPocket(
            InventoryPocketUIView pocket,
            InventoryCompartmentConfig cfg,
            InventoryCompartmentUI ui,
            bool fillPanel)
        {
            if (pocket == null || cfg == null || ui == null) return;
            if (ui.CellImages.Count > 0) return;
            PopulateCells(ui, cfg.gridWidth, cfg.gridHeight, ui.CellSize, fillPanel);
            if (!fillPanel && pocket.transform is RectTransform pocketRt)
                PositionCompartmentPocket(pocketRt, cfg, ui.GridRoot, ui.ItemsRoot);
        }

        private bool ShouldFillPanel(InventoryCompartmentConfig cfg) =>
            gridFillsPanel && cfg != null && GetActiveCompartmentCount() <= 1;

        private int GetActiveCompartmentCount()
        {
            var layout = inventory?.Layout;
            if (layout?.compartments == null || layout.compartments.Length == 0) return 1;
            var count = 0;
            foreach (var c in layout.compartments)
            {
                if (c != null && !string.IsNullOrWhiteSpace(c.id))
                    count++;
            }
            return count;
        }

        private Vector2 GetPanelGridAreaSize()
        {
            if (panelRoot == null) return new Vector2(256f, 192f);
            var size = panelRoot.rect.size;
            if (size.x < 1f || size.y < 1f)
                size = panelRoot.sizeDelta;
            var pad = gridPanelPadding * 2f;
            return new Vector2(Mathf.Max(8f, size.x - pad), Mathf.Max(8f, size.y - pad));
        }

        private void ApplyPanelFillRect(RectTransform pocketRt)
        {
            var pad = gridPanelPadding;
            pocketRt.anchorMin = Vector2.zero;
            pocketRt.anchorMax = Vector2.one;
            pocketRt.pivot = new Vector2(0.5f, 0.5f);
            pocketRt.offsetMin = new Vector2(pad, pad);
            pocketRt.offsetMax = new Vector2(-pad, -pad);
            pocketRt.anchoredPosition = Vector2.zero;
        }

        private float ResolveCellSize(
            RectTransform pocketRt,
            int gridWidth,
            int gridHeight,
            bool fillPanel,
            Vector2 fallbackArea)
        {
            if (cellSizeOverride > 0f)
                return cellSizeOverride;

            var area = fallbackArea;
            if (fillPanel && pocketRt != null)
            {
                Canvas.ForceUpdateCanvases();
                if (panelRoot != null)
                    LayoutRebuilder.ForceRebuildLayoutImmediate(panelRoot);
                area = pocketRt.rect.size;
                if (area.x < 1f || area.y < 1f)
                    area = GetPanelGridAreaSize();
            }

            return ComputeAutoCellSize(area, gridWidth, gridHeight);
        }

        private static void StretchLayerToFillParent(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0f, 1f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
        }

        private static void EnsureGridMask(RectTransform pocketRt)
        {
            if (pocketRt == null || pocketRt.GetComponent<RectMask2D>() != null) return;
            pocketRt.gameObject.AddComponent<RectMask2D>();
        }

        private void PositionCompartmentPocket(
            RectTransform pocketRt,
            InventoryCompartmentConfig cfg,
            RectTransform gridRt,
            RectTransform itemsRt)
        {
            if (pocketRt == null || cfg == null) return;
            if (gridRt != null && gridRt.sizeDelta.sqrMagnitude > 0.01f)
                FitPocketToGrid(pocketRt, gridRt, itemsRt);
            var size = gridRt != null && gridRt.sizeDelta.sqrMagnitude > 0.01f
                ? pocketRt.sizeDelta
                : cfg.slotAreaSize;
            ApplyCompartmentRect(pocketRt, cfg.anchor, cfg.anchoredPosition, size);
        }

        private void RefreshPocketUnlockStates()
        {
            foreach (var p in _pockets)
            {
                if (p != null)
                    p.RefreshUnlockState();
            }
        }

        private static void SetupGridContentRect(RectTransform rt)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = Vector2.zero;
        }

        private static void FitPocketToGrid(RectTransform pocketRt, RectTransform gridRt, RectTransform itemsRt)
        {
            if (gridRt == null || pocketRt == null) return;
            var size = gridRt.sizeDelta;
            pocketRt.sizeDelta = size;
            if (itemsRt != null)
                itemsRt.sizeDelta = size;
        }

        private static void ApplyCompartmentRect(
            RectTransform rt,
            InventoryCompartmentAnchor anchor,
            Vector2 anchoredPosition,
            Vector2 size)
        {
            switch (anchor)
            {
                case InventoryCompartmentAnchor.BottomRight:
                    rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
                    rt.pivot = new Vector2(1f, 0f);
                    break;
                default:
                    rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                    rt.pivot = new Vector2(0f, 1f);
                    break;
            }

            rt.anchoredPosition = anchoredPosition;
            rt.sizeDelta = size;
        }

        private float ComputeAutoCellSize(Vector2 area, int w, int h)
        {
            w = Mathf.Max(1, w);
            h = Mathf.Max(1, h);
            var sx = (area.x - (w - 1) * cellSpacing) / w;
            var sy = (area.y - (h - 1) * cellSpacing) / h;
            return Mathf.Max(8f, Mathf.Min(sx, sy));
        }

        private void PopulateCells(
            InventoryCompartmentUI ui,
            int width,
            int height,
            float cellSize,
            bool stretchGridToPocket)
        {
            if (!stretchGridToPocket)
            {
                ui.GridRoot.sizeDelta = new Vector2(
                    width * (cellSize + cellSpacing) - cellSpacing,
                    height * (cellSize + cellSpacing) - cellSpacing);
            }

            var contentOffset = Vector2.zero;
            if (stretchGridToPocket)
                contentOffset = ComputeContentCenterOffset(ui.GridRoot, width, height, cellSize);

            ui.SetContentOffset(contentOffset);

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var cell = Object.Instantiate(cellPrefab, ui.GridRoot);
                    cell.name = $"Cell_{x}_{y}";
                    var rt = cell.GetComponent<RectTransform>();
                    if (rt != null)
                    {
                        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                        rt.pivot = new Vector2(0f, 1f);
                        rt.sizeDelta = new Vector2(cellSize, cellSize);
                        rt.anchoredPosition = ui.GridToLocal(x, y);
                    }

                    var img = cell.GetComponent<Image>();
                    if (img != null)
                    {
                        img.color = cellEmptyColor;
                        ui.RegisterCell(img);
                    }
                }
            }
        }

        private Vector2 ComputeContentCenterOffset(RectTransform layer, int width, int height, float cellSize)
        {
            Canvas.ForceUpdateCanvases();
            var layerSize = layer.rect.size;
            var content = new Vector2(
                width * (cellSize + cellSpacing) - cellSpacing,
                height * (cellSize + cellSpacing) - cellSpacing);
            return new Vector2(
                Mathf.Max(0f, (layerSize.x - content.x) * 0.5f),
                Mathf.Min(0f, -(layerSize.y - content.y) * 0.5f));
        }

        internal InventoryCompartmentUI GetCompartmentForItem(InventoryItemInstance item)
        {
            if (item == null || !item.IsPlaced) return null;
            foreach (var c in _compartments)
            {
                if (c.CompartmentId == item.compartmentId)
                    return c;
            }
            return _legacyCompartment;
        }

        public void BeginDrag(InventoryItemView view)
        {
            if (inventory?.Bag == null) return;
            _dragging = view;
            _dragHadPlacement = view.Item.IsPlaced;
            _dragStartCompartmentId = view.Item.compartmentId;
            _dragStartX = view.Item.gridX;
            _dragStartY = view.Item.gridY;
            _dragStartRotation = view.Item.rotation;
            _dragGrabOffset = Vector2Int.zero;
            var cam = panelRoot?.GetComponentInParent<Canvas>()?.worldCamera;
            if (_dragHadPlacement && view.Item.definition != null &&
                Mouse.current != null &&
                ScreenPointToCompartment(Mouse.current.position.ReadValue(), cam, out _, out var hx, out var hy))
            {
                _dragGrabOffset = InventoryDragPlacement.ComputeGrabOffset(
                    view.Item.definition.shape, view.Item.rotation,
                    _dragStartX, _dragStartY, hx, hy);
            }

            inventory.DetachForDrag(view.Item);
            MoveDragViewToCanvas(view);
            view.transform.SetAsLastSibling();
            UpdatePlacementPreview(view);
        }

        public void Dragging(InventoryItemView view, PointerEventData eventData)
        {
            if (_dragging != view) return;

            if (TryGetDragAnchor(eventData.position, eventData.pressEventCamera, view,
                    out var compartment, out _, out var gx, out var gy))
            {
                SnapDragViewToGrid(view, compartment, gx, gy);
            }
            else if (_crossPanelDropHandler != null)
            {
                FollowPointerOnCanvas(view, eventData);
            }
            else
            {
                ClampDragToCompartment(view, eventData);
            }

            UpdatePlacementPreview(view);
        }

        private void FollowPointerOnCanvas(InventoryItemView view, PointerEventData eventData)
        {
            if (view == null || view.RectTransform == null) return;
            var canvas = panelRoot?.GetComponentInParent<Canvas>();
            if (canvas == null) return;

            var canvasRoot = canvas.transform as RectTransform;
            if (canvasRoot == null) return;

            var canvasCam = canvas.worldCamera;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRoot, eventData.position, canvasCam ?? eventData.pressEventCamera, out var local))
                return;

            view.RectTransform.SetParent(canvasRoot, true);

            var offset = Vector2.zero;
            if (view.Item?.definition != null && view.Compartment != null)
            {
                var step = view.Compartment.CellSize + view.Compartment.CellSpacing;
                offset = new Vector2(_dragGrabOffset.x * step, -_dragGrabOffset.y * step);
            }

            view.RectTransform.anchoredPosition = local - offset;
            if (view.Item?.definition != null)
                view.RectTransform.sizeDelta = view.Compartment?.GetItemSize(view.Item.definition.shape, view.Item.rotation) ?? view.RectTransform.sizeDelta;
        }

        private Camera GetCanvasCamera() => panelRoot?.GetComponentInParent<Canvas>()?.worldCamera;

        private void SnapDragViewToGrid(InventoryItemView view, InventoryCompartmentUI compartment, int gx, int gy)
        {
            if (view == null || compartment == null || view.Item?.definition == null) return;
            if (view.RectTransform.parent == compartment.ItemsRoot)
            {
                compartment.SnapItemToGrid(view.Item, gx, gy, view.RectTransform);
                return;
            }

            var targetWorld = compartment.GridRoot.TransformPoint(compartment.GridToAnchoredPosition(gx, gy, view.Item.definition.shape, view.Item.rotation));
            var canvasRoot = view.RectTransform.parent as RectTransform;
            if (canvasRoot == null)
            {
                compartment.SnapItemToGrid(view.Item, gx, gy, view.RectTransform);
                return;
            }

            var canvas = canvasRoot.GetComponentInParent<Canvas>();
            var canvasCam = canvas?.worldCamera;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRoot,
                    RectTransformUtility.WorldToScreenPoint(canvasCam, targetWorld),
                    canvasCam,
                    out var local))
            {
                view.RectTransform.anchoredPosition = local;
                view.RectTransform.sizeDelta = compartment.GetItemSize(view.Item.definition.shape, view.Item.rotation);
            }
        }

        public void EndDrag(InventoryItemView view, PointerEventData eventData)
        {
            if (_dragging != view) return;

            var placed = false;
            if (TryGetDragAnchor(eventData.position, eventData.pressEventCamera, view,
                    out _, out var compartmentId, out var gx, out var gy) &&
                inventory.Bag.CanPlace(view.Item, compartmentId, gx, gy, view.Item.rotation))
            {
                placed = inventory.TryPlace(view.Item, compartmentId, gx, gy, view.Item.rotation);
            }

            if (!placed && _crossPanelDropHandler != null)
                placed = _crossPanelDropHandler(view, eventData);

            if (!placed && _dragHadPlacement)
            {
                view.Item.rotation = _dragStartRotation;
                inventory.TryPlace(view.Item, _dragStartCompartmentId, _dragStartX, _dragStartY, _dragStartRotation);
            }
            else if (!placed && !_dragHadPlacement)
            {
                inventory.RemoveItem(view.Item);
            }

            _dragging = null;
            ResetAllCellColors();
            RefreshAll();
        }

        private bool TryGetDragAnchor(
            Vector2 screenPosition,
            Camera cam,
            InventoryItemView view,
            out InventoryCompartmentUI compartment,
            out string compartmentId,
            out int gx,
            out int gy)
        {
            compartment = null;
            compartmentId = null;
            gx = gy = -1;

            if (ScreenPointToCompartment(screenPosition, cam, out compartmentId, out var hoverX, out var hoverY))
            {
                compartment = GetCompartmentById(compartmentId);
                if (compartment == null) return false;

                if (view?.Item?.definition != null)
                {
                    var cid = compartmentId;
                    InventoryDragPlacement.TryGetAnchorFromHover(
                        view.Item.definition.shape,
                        view.Item.rotation,
                        hoverX,
                        hoverY,
                        _dragGrabOffset,
                        (ax, ay) => inventory.Bag.CanPlace(view.Item, cid, ax, ay, view.Item.rotation),
                        out gx,
                        out gy);
                }
                else
                {
                    gx = hoverX;
                    gy = hoverY;
                }

                return true;
            }

            return false;
        }

        private void ClampDragToCompartment(InventoryItemView view, PointerEventData eventData)
        {
            var compartment = view.Compartment ?? GetCompartmentById(_dragStartCompartmentId);
            var dragParent = view.RectTransform.parent as RectTransform ?? compartment?.ItemsRoot ?? itemsRoot;
            if (dragParent == null) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    dragParent, eventData.position, eventData.pressEventCamera, out var local))
                return;

            var rect = dragParent.rect;
            local.x = Mathf.Clamp(local.x, rect.xMin, rect.xMax);
            local.y = Mathf.Clamp(local.y, rect.yMin, rect.yMax);
            view.RectTransform.anchoredPosition = local;
        }

        internal void CancelDrag()
        {
            if (_dragging == null) return;
            var v = _dragging;
            _dragging = null;
            if (v.Item != null && _dragHadPlacement)
            {
                v.Item.rotation = _dragStartRotation;
                inventory.TryPlace(v.Item, _dragStartCompartmentId, _dragStartX, _dragStartY, _dragStartRotation);
            }
            else if (v.Item != null && !_dragHadPlacement)
            {
                inventory.RemoveItem(v.Item);
            }
            ResetAllCellColors();
            RefreshAll();
        }

        private void MoveDragViewToCanvas(InventoryItemView view)
        {
            if (view == null || panelRoot == null) return;
            var canvas = panelRoot.GetComponentInParent<Canvas>();
            if (canvas == null) return;

            var canvasTransform = canvas.transform;
            if (view.transform.parent == canvasTransform) return;
            view.transform.SetParent(canvasTransform, true);
        }

        private bool ScreenPointToCompartment(Vector2 screen, Camera cam, out string compartmentId, out int gx, out int gy)
        {
            compartmentId = null;
            gx = gy = -1;

            foreach (var c in _compartments)
            {
                if (inventory != null && !inventory.IsCompartmentUnlocked(c.CompartmentId))
                    continue;

                if (c.ScreenPointToGrid(screen, cam, out gx, out gy))
                {
                    compartmentId = c.CompartmentId;
                    return true;
                }
            }

            if (_legacyCompartment != null && _legacyCompartment.ScreenPointToGrid(screen, cam, out gx, out gy))
            {
                compartmentId = _legacyCompartment.CompartmentId;
                return true;
            }

            return false;
        }

        private void UpdatePlacementPreview(InventoryItemView view)
        {
            ResetAllCellColors();
            if (view?.Item?.definition == null || inventory?.Bag == null) return;

            if (Mouse.current == null ||
                !TryGetDragAnchor(Mouse.current.position.ReadValue(), null, view,
                    out var compartment, out var cid, out var gx, out var gy))
                return;

            if (compartment == null || !inventory.IsCompartmentUnlocked(cid)) return;

            var valid = inventory.Bag.CanPlace(view.Item, cid, gx, gy, view.Item.rotation);
            var offsets = new List<Vector2Int>();
            view.Item.definition.shape.GetOccupiedOffsets(view.Item.rotation, offsets);

            var cells = compartment.CellImages;
            foreach (var o in offsets)
            {
                var x = gx + o.x;
                var y = gy + o.y;
                var idx = y * compartment.Grid.Width + x;
                if (idx < 0 || idx >= cells.Count) continue;
                cells[idx].color = valid ? cellHoverValidColor : cellHoverInvalidColor;
            }
        }

        private InventoryCompartmentUI GetCompartmentById(string id)
        {
            foreach (var c in _compartments)
            {
                if (c.CompartmentId == id) return c;
            }
            return _legacyCompartment;
        }

        private void ResetAllCellColors()
        {
            foreach (var c in _compartments)
                c.ResetCellColors(cellEmptyColor);
            _legacyCompartment?.ResetCellColors(cellEmptyColor);
        }

        private void RefreshAll()
        {
            if (!_isOpen || inventory?.Bag == null || itemViewPrefab == null)
                return;

            BuildCompartments();
            RefreshPocketUnlockStates();

            var live = new HashSet<string>();
            foreach (var item in inventory.Bag.AllItems)
            {
                if (item?.definition == null) continue;
                live.Add(item.instanceId);

                var compartment = GetCompartmentForItem(item);
                var parent = compartment?.ItemsRoot ?? itemsRoot;
                if (parent == null) continue;

                if (!_views.TryGetValue(item.instanceId, out var view))
                {
                    var go = Object.Instantiate(itemViewPrefab, parent);
                    view = go.GetComponent<InventoryItemView>();
                    if (view == null)
                        view = go.AddComponent<InventoryItemView>();
                    view.Init(this, item, compartment);
                    _views[item.instanceId] = view;
                }
                else if (view.transform.parent != parent)
                {
                    view.transform.SetParent(parent, false);
                    view.SetCompartment(compartment);
                }

                view.SyncFromItem();
            }

            var toRemove = new List<string>();
            foreach (var kv in _views)
            {
                if (!live.Contains(kv.Key))
                    toRemove.Add(kv.Key);
            }

            foreach (var id in toRemove)
            {
                if (_views.TryGetValue(id, out var v) && v != null)
                    Destroy(v.gameObject);
                _views.Remove(id);
            }
        }
    }
}
