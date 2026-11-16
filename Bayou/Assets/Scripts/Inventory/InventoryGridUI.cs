using UnityEngine;
using UnityEngine.UI;

namespace Bayou.Inventory
{
    /// <summary>
    /// Fills a handmade <see cref="GridLayoutGroup"/> with your Cell prefab and scales it to the panel.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(GridLayoutGroup))]
    public sealed class InventoryGridUI : MonoBehaviour
    {
        [Header("Grid")]
        [SerializeField] private InventoryCellUI cellPrefab;
        [SerializeField] private int columns = 7;
        [SerializeField] private int rows = 6;

        [Header("Fill panel")]
        [SerializeField] private bool fillParent = true;
        [SerializeField] private float padLeft = 28f;
        [SerializeField] private float padRight = 28f;
        [SerializeField] private float padTop = 28f;
        [SerializeField] private float padBottom = 28f;
        [SerializeField] private float cellSpacing = 6f;
        [SerializeField] private bool forceSquareCells = true;
        [SerializeField] private Color cellColor = new(0f, 0f, 0f, 0.42f);
        [SerializeField] private bool backpackFrame;

        // Fractions of the 900×700 backpack panel (art is stretched, preserveAspect off).
        // Measured on BackpackBackground so the centered 7×6 block sits in the dark
        // opening, clear of the rosary, cloth, flask, top buckle, and bottom belt.
        // The panel itself is not resized.
        private static readonly Vector2 OpeningAnchorMin = new(0.24f, 0.22f);
        private static readonly Vector2 OpeningAnchorMax = new(0.75f, 0.82f);
        private static readonly Color EmptyCell = new(0f, 0f, 0f, 0.42f);

        private InventoryCellUI[,] _cells;
        private GridLayoutGroup _layout;
        private bool _built;
        private RectTransform _rect;

        public int Columns => columns;
        public int Rows => rows;
        public GridLayoutGroup Layout => _layout != null ? _layout : GetComponent<GridLayoutGroup>();

        private void Awake()
        {
            _layout = GetComponent<GridLayoutGroup>();
            _rect = transform as RectTransform;
        }

        private void Start() => EnsureBuilt();

        public void ConfigureSize(int width, int height)
        {
            columns = Mathf.Max(1, width);
            rows = Mathf.Max(1, height);
            _built = false;
            EnsureBuilt();
        }

        public void EnsureBuilt()
        {
            if (_built && _cells != null)
                return;

            BuildGrid();
        }

        private void BuildGrid()
        {
            if (cellPrefab == null)
            {
                Debug.LogWarning("[Inventory] InventoryGridUI missing cellPrefab.");
                return;
            }

            if (_layout == null) _layout = GetComponent<GridLayoutGroup>();
            if (_rect == null) _rect = transform as RectTransform;

            ApplyFillLayout();

            foreach (Transform child in transform)
                Destroy(child.gameObject);

            _cells = new InventoryCellUI[columns, rows];

            for (var y = 0; y < rows; y++)
            for (var x = 0; x < columns; x++)
            {
                var cell = Instantiate(cellPrefab, transform);
                cell.gameObject.SetActive(true);
                cell.Setup(x, y);
                var img = cell.GetComponent<Image>();
                if (img != null)
                    img.raycastTarget = true;
                cell.SetBaseColor(cellColor);

                _cells[x, y] = cell;
            }

            _built = true;
            if (backpackFrame)
                TintCells();
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_rect);
            ApplyFillLayout();
        }

        private void TintCells()
        {
            if (_cells == null) return;
            for (var y = 0; y < rows; y++)
            for (var x = 0; x < columns; x++)
            {
                if (_cells[x, y] != null)
                    _cells[x, y].SetBaseColor(cellColor);
            }
        }

        /// <summary>
        /// Keeps the backpack illustration as the panel background and seats the grid in the dark center.
        /// </summary>
        public void UseBackpackFrame()
        {
            backpackFrame = true;
            // See-through black so the leather shows through. Cream cells read as yellow.
            cellColor = EmptyCell;
            TintCells();
            ApplyFillLayout();
        }

        public void ApplyFillLayout()
        {
            if (_layout == null) _layout = GetComponent<GridLayoutGroup>();
            if (_rect == null) _rect = transform as RectTransform;
            if (_rect == null) return;

            if (backpackFrame || ParentShowsBackpack())
            {
                // Seat only this rect in the opening. Never touch the parent panel size.
                backpackFrame = true;
                cellColor = EmptyCell;
                _rect.anchorMin = OpeningAnchorMin;
                _rect.anchorMax = OpeningAnchorMax;
                _rect.pivot = new Vector2(0.5f, 0.5f);
                _rect.offsetMin = Vector2.zero;
                _rect.offsetMax = Vector2.zero;
                _rect.anchoredPosition = Vector2.zero;
                _rect.sizeDelta = Vector2.zero;
                _rect.localScale = Vector3.one;
                _rect.localRotation = Quaternion.identity;
                var stray = _rect.GetComponent<Image>();
                if (stray != null)
                {
                    stray.color = new Color(0f, 0f, 0f, 0f);
                    stray.raycastTarget = false;
                }

                if (_rect.GetComponent<RectMask2D>() == null)
                    _rect.gameObject.AddComponent<RectMask2D>();
            }
            else if (fillParent && _rect.parent is RectTransform)
            {
                // Top-left pivot so GridLayoutGroup UpperLeft matches item index math.
                _rect.anchorMin = Vector2.zero;
                _rect.anchorMax = Vector2.one;
                _rect.pivot = new Vector2(0f, 1f);
                _rect.offsetMin = new Vector2(padLeft, padBottom);
                _rect.offsetMax = new Vector2(-padRight, -padTop);
                _rect.localScale = Vector3.one;
                _rect.localRotation = Quaternion.identity;
            }

            Canvas.ForceUpdateCanvases();

            // Parent span, not _rect.rect. Reading the rect in the same frame the
            // anchors change still returns the old full-panel size, and those huge
            // cells then paint across the straps.
            if (!TryGetArrangedSize(out var width, out var height))
                return;

            var spacing = Mathf.Max(0f, cellSpacing);
            var innerW = Mathf.Max(0f, width - spacing * (columns - 1));
            var innerH = Mathf.Max(0f, height - spacing * (rows - 1));
            var cellW = innerW / columns;
            var cellH = innerH / rows;

            if (forceSquareCells)
            {
                var side = Mathf.Floor(Mathf.Min(cellW, cellH));
                cellW = side;
                cellH = side;
            }

            cellW = Mathf.Max(12f, cellW);
            cellH = Mathf.Max(12f, cellH);

            var padX = 0;
            var padY = 0;
            if (backpackFrame)
            {
                var usedW = columns * cellW + (columns - 1) * spacing;
                var usedH = rows * cellH + (rows - 1) * spacing;
                padX = Mathf.Max(0, Mathf.FloorToInt((width - usedW) * 0.5f));
                padY = Mathf.Max(0, Mathf.FloorToInt((height - usedH) * 0.5f));
            }

            _layout.padding = new RectOffset(padX, padX, padY, padY);
            _layout.spacing = new Vector2(spacing, spacing);
            _layout.cellSize = new Vector2(cellW, cellH);
            _layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            _layout.constraintCount = columns;
            _layout.startCorner = GridLayoutGroup.Corner.UpperLeft;
            _layout.startAxis = GridLayoutGroup.Axis.Horizontal;
            _layout.childAlignment = TextAnchor.UpperLeft;

            LayoutRebuilder.ForceRebuildLayoutImmediate(_rect);
            if (backpackFrame)
                TintCells();
        }

        private bool TryGetArrangedSize(out float width, out float height)
        {
            width = 0f;
            height = 0f;
            if (_rect == null) return false;

            var parent = _rect.parent as RectTransform;
            if (parent != null && parent.rect.width >= 16f && parent.rect.height >= 16f)
            {
                width = parent.rect.width * Mathf.Abs(_rect.anchorMax.x - _rect.anchorMin.x) + _rect.sizeDelta.x;
                height = parent.rect.height * Mathf.Abs(_rect.anchorMax.y - _rect.anchorMin.y) + _rect.sizeDelta.y;
            }
            else
            {
                width = _rect.rect.width;
                height = _rect.rect.height;
            }

            return width >= 16f && height >= 16f;
        }

        private bool ParentShowsBackpack()
        {
            var parentImage = _rect != null && _rect.parent != null
                ? _rect.parent.GetComponent<Image>()
                : null;
            var spriteName = parentImage != null && parentImage.sprite != null
                ? parentImage.sprite.name
                : null;
            return !string.IsNullOrEmpty(spriteName) &&
                   spriteName.IndexOf("Backpack", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public Vector2 CellTopLeftLocal(int x, int y)
        {
            var layout = Layout;
            var padL = layout != null ? layout.padding.left : 0;
            var padT = layout != null ? layout.padding.top : 0;
            var step = GetCellStep();
            return new Vector2(padL + x * step.x, -padT - y * step.y);
        }

        public Vector2 ItemSize(int boundW, int boundH)
        {
            var layout = Layout;
            var cellSize = layout != null ? layout.cellSize : new Vector2(64f, 64f);
            var spacing = layout != null ? layout.spacing : Vector2.zero;
            return new Vector2(
                boundW * cellSize.x + Mathf.Max(0, boundW - 1) * spacing.x,
                boundH * cellSize.y + Mathf.Max(0, boundH - 1) * spacing.y);
        }

        public InventoryCellUI GetCell(int x, int y)
        {
            EnsureBuilt();
            if (_cells == null || x < 0 || y < 0 || x >= columns || y >= rows)
                return null;
            return _cells[x, y];
        }

        public bool TryGetCellAtScreenPoint(Vector2 screen, Camera cam, out InventoryCellUI cell)
        {
            EnsureBuilt();
            cell = null;
            if (_cells == null) return false;

            InventoryCellUI nearest = null;
            var nearestDist = float.MaxValue;

            for (var y = 0; y < rows; y++)
            for (var x = 0; x < columns; x++)
            {
                var c = _cells[x, y];
                if (c?.Rect == null) continue;
                if (RectTransformUtility.RectangleContainsScreenPoint(c.Rect, screen, cam))
                {
                    cell = c;
                    return true;
                }

                var corners = new Vector3[4];
                c.Rect.GetWorldCorners(corners);
                var worldCenter = (corners[0] + corners[2]) * 0.5f;
                var screenCenter = RectTransformUtility.WorldToScreenPoint(cam, worldCenter);
                var dist = ((Vector2)screenCenter - screen).sqrMagnitude;
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = c;
                }
            }

            // Pointer is in the gutter between rows/columns — still snap to the closest cell.
            if (nearest != null && _rect != null &&
                RectTransformUtility.RectangleContainsScreenPoint(_rect, screen, cam))
            {
                cell = nearest;
                return true;
            }

            return false;
        }

        public void ClearHighlights()
        {
            if (_cells == null) return;
            for (var y = 0; y < rows; y++)
            for (var x = 0; x < columns; x++)
                _cells[x, y]?.ClearHighlight();
        }

        public void HighlightShape(int anchorX, int anchorY, ItemShape shape, int rotation, Color color, bool valid)
        {
            ClearHighlights();
            var offsets = new System.Collections.Generic.List<Vector2Int>();
            shape.GetOccupiedOffsets(rotation, offsets);
            var c = valid ? color : new Color(0.55f, 0.15f, 0.12f, 0.9f);
            foreach (var o in offsets)
                GetCell(anchorX + o.x, anchorY + o.y)?.SetHighlight(c);
        }

        public Vector2 GetCellStep()
        {
            var layout = Layout;
            if (layout == null) return new Vector2(64f, 64f);
            return layout.cellSize + layout.spacing;
        }
    }
}
