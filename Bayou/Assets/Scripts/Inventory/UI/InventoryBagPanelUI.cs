#if !ENABLE_INPUT_SYSTEM
#error InventoryBagPanelUI requires the New Input System (ENABLE_INPUT_SYSTEM).
#endif

using System;
using System.Collections;
using System.Collections.Generic;
using Bayou.Inventory;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Bayou.Inventory.UI
{
    /// <summary>
    /// RE-style grid panel for a single <see cref="InventoryBagModel"/> — used by shop dual-pane UI.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InventoryBagPanelUI : MonoBehaviour, IInventoryDragHost
    {
        [SerializeField] private RectTransform panelRoot;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private GameObject cellPrefab;
        [SerializeField] private GameObject itemViewPrefab;

        [Header("Layout")]
        [SerializeField] private BackpackLayoutDefinition layout;
        [SerializeField] private float cellSpacing = 2f;
        [SerializeField] private float cellSizeOverride;
        [SerializeField] private bool gridFillsPanel = true;
        [SerializeField] private float gridPanelPadding = 12f;
        [SerializeField] private bool clipItemsToGrid = true;

        [Header("Colors")]
        [SerializeField] private Color cellEmptyColor = new(0.12f, 0.14f, 0.16f, 0.55f);
        [SerializeField] private Color cellHoverValidColor = new(0.2f, 0.45f, 0.25f, 0.85f);
        [SerializeField] private Color cellHoverInvalidColor = new(0.5f, 0.15f, 0.12f, 0.85f);

        private readonly List<InventoryCompartmentUI> _compartments = new();
        private readonly Dictionary<string, InventoryItemView> _views = new();

        private InventoryBagModel _bag;
        private Func<string, bool> _compartmentUnlocked = _ => true;
        private Func<InventoryItemView, PointerEventData, bool> _crossPanelDropHandler;

        private InventoryItemView _dragging;
        private bool _dragHadPlacement;
        private string _dragStartCompartmentId;
        private int _dragStartX;
        private int _dragStartY;
        private int _dragStartRotation;
        private Vector2Int _dragGrabOffset;
        private Coroutine _layoutRebuildCoroutine;

        public RectTransform PanelRoot => panelRoot;
        public InventoryBagModel Bag => _bag;

        public void Configure(
            InventoryBagModel bag,
            BackpackLayoutDefinition layoutOverride = null,
            Func<string, bool> compartmentUnlocked = null)
        {
            _bag = bag;
            if (layoutOverride != null)
                layout = layoutOverride;
            _compartmentUnlocked = compartmentUnlocked ?? (_ => true);
            ApplyBackground();
            ScheduleLayoutRebuild();
        }

        public void SetCrossPanelDropHandler(Func<InventoryItemView, PointerEventData, bool> handler) =>
            _crossPanelDropHandler = handler;

        public void Refresh() => RefreshAll();

        public void CancelDrag() => CancelActiveDrag();

        public void TryRotateDraggedItem()
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

        public bool ScreenPointToGrid(Vector2 screen, Camera cam, out string compartmentId, out int gx, out int gy)
        {
            compartmentId = null;
            gx = gy = -1;

            foreach (var c in _compartments)
            {
                if (!_compartmentUnlocked(c.CompartmentId))
                    continue;

                if (c.ScreenPointToGrid(screen, cam, out gx, out gy))
                {
                    compartmentId = c.CompartmentId;
                    return true;
                }
            }

            return false;
        }

        public bool ContainsScreenPoint(Vector2 screen, Camera cam)
        {
            if (panelRoot == null) return false;
            return RectTransformUtility.RectangleContainsScreenPoint(panelRoot, screen, cam);
        }

        public bool TryPlace(InventoryItemInstance item, string compartmentId, int x, int y, int rotation)
        {
            if (_bag == null || item == null) return false;
            if (!_compartmentUnlocked(compartmentId)) return false;
            if (!_bag.TryPlace(item, compartmentId, x, y, rotation)) return false;
            return true;
        }

        public void DetachForDrag(InventoryItemInstance item)
        {
            _bag?.DetachFromGrid(item);
        }

        public void BeginDrag(InventoryItemView view)
        {
            if (_bag == null) return;
            _dragging = view;
            _dragHadPlacement = view.Item.IsPlaced;
            _dragStartCompartmentId = view.Item.compartmentId;
            _dragStartX = view.Item.gridX;
            _dragStartY = view.Item.gridY;
            _dragStartRotation = view.Item.rotation;
            _dragGrabOffset = Vector2Int.zero;
            if (_dragHadPlacement && view.Item.definition != null &&
                Mouse.current != null &&
                ScreenPointToGrid(Mouse.current.position.ReadValue(), null, out _, out var hx, out var hy))
            {
                _dragGrabOffset = InventoryDragPlacement.ComputeGrabOffset(
                    view.Item.definition.shape, view.Item.rotation,
                    _dragStartX, _dragStartY, hx, hy);
            }

            DetachForDrag(view.Item);
            view.transform.SetAsLastSibling();
            UpdatePlacementPreview(view);
        }

        public void Dragging(InventoryItemView view, PointerEventData eventData)
        {
            if (_dragging != view) return;

            if (TryGetDragAnchor(eventData.position, eventData.pressEventCamera, view,
                    out var compartment, out _, out var gx, out var gy))
            {
                compartment.SnapItemToGrid(view.Item, gx, gy, view.RectTransform);
            }
            else
            {
                ClampDragToCompartment(view, eventData);
            }

            UpdatePlacementPreview(view);
        }

        public void EndDrag(InventoryItemView view, PointerEventData eventData)
        {
            if (_dragging != view) return;

            var placed = false;
            if (TryGetDragAnchor(eventData.position, eventData.pressEventCamera, view,
                    out _, out var compartmentId, out var gx, out var gy) &&
                _bag.CanPlace(view.Item, compartmentId, gx, gy, view.Item.rotation))
            {
                placed = TryPlace(view.Item, compartmentId, gx, gy, view.Item.rotation);
            }

            if (!placed && _crossPanelDropHandler != null)
                placed = _crossPanelDropHandler(view, eventData);

            if (!placed && _dragHadPlacement)
            {
                view.Item.rotation = _dragStartRotation;
                TryPlace(view.Item, _dragStartCompartmentId, _dragStartX, _dragStartY, _dragStartRotation);
            }
            else if (!placed && !_dragHadPlacement)
            {
                _bag?.Remove(view.Item);
            }

            _dragging = null;
            ResetAllCellColors();
            RefreshAll();
        }

        private void ApplyBackground()
        {
            if (backgroundImage == null || layout == null) return;

            if (layout.backgroundSprite != null)
            {
                backgroundImage.sprite = layout.backgroundSprite;
                backgroundImage.preserveAspect = true;
                backgroundImage.color = Color.white;
            }
            else
            {
                backgroundImage.sprite = null;
                backgroundImage.color = new Color(0.08f, 0.09f, 0.11f, 0.94f);
            }
        }

        private void ScheduleLayoutRebuild()
        {
            if (_layoutRebuildCoroutine != null)
                StopCoroutine(_layoutRebuildCoroutine);
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
            for (var i = panelRoot.childCount - 1; i >= 0; i--)
            {
                var child = panelRoot.GetChild(i);
                if (child.name.StartsWith("Pocket_", StringComparison.Ordinal))
                    Destroy(child.gameObject);
            }

            _compartments.Clear();
        }

        private void BuildCompartments()
        {
            if (_bag == null || cellPrefab == null || panelRoot == null || _compartments.Count > 0)
                return;

            if (layout?.compartments != null && layout.compartments.Length > 0)
            {
                var primary = GetPrimaryCompartmentConfig(layout);
                if (primary != null && _bag.TryGetGrid(primary.id, out var primaryGrid))
                    BuildOneCompartment(primary, primaryGrid);
                return;
            }

            var grid = _bag.CompartmentIds.Count > 0 ? _bag.GetGrid(_bag.CompartmentIds[0]) : null;
            if (grid == null) return;

            var cfg = new InventoryCompartmentConfig
            {
                id = _bag.CompartmentIds[0],
                gridWidth = grid.Width,
                gridHeight = grid.Height,
                fillPanel = true
            };
            BuildOneCompartment(cfg, grid);
        }

        private void BuildOneCompartment(InventoryCompartmentConfig cfg, InventoryGridModel grid)
        {
            var pocket = new GameObject($"Pocket_{cfg.id}", typeof(RectTransform));
            var pocketRt = pocket.GetComponent<RectTransform>();
            pocketRt.SetParent(panelRoot, false);
            ApplyPanelFillRect(pocketRt);

            var gridGo = new GameObject("Grid", typeof(RectTransform));
            var gridRt = gridGo.GetComponent<RectTransform>();
            gridRt.SetParent(pocketRt, false);

            var itemsGo = new GameObject("Items", typeof(RectTransform));
            var itemsRt = itemsGo.GetComponent<RectTransform>();
            itemsRt.SetParent(pocketRt, false);

            StretchLayerToFillParent(gridRt);
            StretchLayerToFillParent(itemsRt);

            var cellSize = ResolveCellSize(pocketRt, cfg.gridWidth, cfg.gridHeight);
            var ui = new InventoryCompartmentUI(cfg.id, gridRt, itemsRt, grid, cellSize, cellSpacing);
            PopulateCells(ui, cfg.gridWidth, cfg.gridHeight, cellSize);
            if (clipItemsToGrid)
                EnsureGridMask(pocketRt);
            _compartments.Add(ui);
        }

        private static InventoryCompartmentConfig GetPrimaryCompartmentConfig(BackpackLayoutDefinition layoutDef)
        {
            if (layoutDef?.compartments == null) return null;
            foreach (var c in layoutDef.compartments)
            {
                if (c != null && c.fillPanel)
                    return c;
            }

            foreach (var c in layoutDef.compartments)
            {
                if (c != null && !string.IsNullOrWhiteSpace(c.id))
                    return c;
            }

            return null;
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

        private float ResolveCellSize(RectTransform pocketRt, int gridWidth, int gridHeight)
        {
            if (cellSizeOverride > 0f)
                return cellSizeOverride;

            Canvas.ForceUpdateCanvases();
            var area = pocketRt.rect.size;
            if (area.x < 1f || area.y < 1f)
                area = panelRoot != null ? panelRoot.rect.size : new Vector2(256f, 192f);

            return ComputeAutoCellSize(area, gridWidth, gridHeight);
        }

        private float ComputeAutoCellSize(Vector2 area, int w, int h)
        {
            w = Mathf.Max(1, w);
            h = Mathf.Max(1, h);
            var sx = (area.x - (w - 1) * cellSpacing) / w;
            var sy = (area.y - (h - 1) * cellSpacing) / h;
            return Mathf.Max(8f, Mathf.Min(sx, sy));
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

        private void PopulateCells(InventoryCompartmentUI ui, int width, int height, float cellSize)
        {
            var contentOffset = ComputeContentCenterOffset(ui.GridRoot, width, height, cellSize);
            ui.SetContentOffset(contentOffset);

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var cell = Instantiate(cellPrefab, ui.GridRoot);
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
            return null;
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

            if (ScreenPointToGrid(screenPosition, cam, out compartmentId, out var hoverX, out var hoverY))
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
                        (ax, ay) => _bag.CanPlace(view.Item, cid, ax, ay, view.Item.rotation),
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
            var dragParent = compartment?.ItemsRoot;
            if (dragParent == null) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    dragParent, eventData.position, eventData.pressEventCamera, out var local))
                return;

            var max = compartment != null ? compartment.GridPixelSize : dragParent.rect.size;
            local.x = Mathf.Clamp(local.x, 0f, max.x);
            local.y = Mathf.Clamp(local.y, -max.y, 0f);
            view.RectTransform.anchoredPosition = local;
        }

        private void CancelActiveDrag()
        {
            if (_dragging == null) return;
            var v = _dragging;
            _dragging = null;
            if (v.Item != null && _dragHadPlacement)
            {
                v.Item.rotation = _dragStartRotation;
                TryPlace(v.Item, _dragStartCompartmentId, _dragStartX, _dragStartY, _dragStartRotation);
            }
            ResetAllCellColors();
            RefreshAll();
        }

        private InventoryCompartmentUI GetCompartmentById(string id)
        {
            foreach (var c in _compartments)
            {
                if (c.CompartmentId == id) return c;
            }
            return null;
        }

        private void UpdatePlacementPreview(InventoryItemView view)
        {
            ResetAllCellColors();
            if (view?.Item?.definition == null || _bag == null) return;

            if (Mouse.current == null ||
                !TryGetDragAnchor(Mouse.current.position.ReadValue(), null, view,
                    out var compartment, out var cid, out var gx, out var gy))
                return;

            if (compartment == null || !_compartmentUnlocked(cid)) return;

            var valid = _bag.CanPlace(view.Item, cid, gx, gy, view.Item.rotation);
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

        private void ResetAllCellColors()
        {
            foreach (var c in _compartments)
                c.ResetCellColors(cellEmptyColor);
        }

        private void RefreshAll()
        {
            if (_bag == null || itemViewPrefab == null || panelRoot == null)
                return;

            BuildCompartments();

            var live = new HashSet<string>();
            foreach (var item in _bag.AllItems)
            {
                if (item?.definition == null) continue;
                live.Add(item.instanceId);

                var compartment = GetCompartmentForItem(item);
                var parent = compartment?.ItemsRoot ?? panelRoot;
                if (parent == null) continue;

                if (!_views.TryGetValue(item.instanceId, out var view))
                {
                    var go = Instantiate(itemViewPrefab, parent);
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
