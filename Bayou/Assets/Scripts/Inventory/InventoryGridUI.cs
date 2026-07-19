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
        [SerializeField] private Color cellColor = new(0.92f, 0.88f, 0.78f, 1f);

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
            {
                ApplyFillLayout();
                return;
            }

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
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_rect);
            ApplyFillLayout();
        }

        public void ApplyFillLayout()
        {
            if (_layout == null) _layout = GetComponent<GridLayoutGroup>();
            if (_rect == null) _rect = transform as RectTransform;
            if (_rect == null) return;

            if (fillParent && _rect.parent is RectTransform)
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

            var width = _rect.rect.width;
            var height = _rect.rect.height;
            if (width < 16f || height < 16f)
                return; // panel not laid out yet (closed / inactive)

            var spacing = Mathf.Max(0f, cellSpacing);
            var cellW = (width - spacing * (columns - 1)) / columns;
            var cellH = (height - spacing * (rows - 1)) / rows;

            if (forceSquareCells)
            {
                var side = Mathf.Floor(Mathf.Min(cellW, cellH));
                cellW = side;
                cellH = side;
            }

            cellW = Mathf.Max(12f, cellW);
            cellH = Mathf.Max(12f, cellH);

            _layout.padding = new RectOffset(0, 0, 0, 0);
            _layout.spacing = new Vector2(spacing, spacing);
            _layout.cellSize = new Vector2(cellW, cellH);
            _layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            _layout.constraintCount = columns;
            _layout.startCorner = GridLayoutGroup.Corner.UpperLeft;
            _layout.startAxis = GridLayoutGroup.Axis.Horizontal;
            _layout.childAlignment = TextAnchor.UpperLeft;

            LayoutRebuilder.ForceRebuildLayoutImmediate(_rect);
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
