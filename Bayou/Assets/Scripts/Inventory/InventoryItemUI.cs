using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Bayou.Inventory
{
    [RequireComponent(typeof(Image))]
    [RequireComponent(typeof(RectTransform))]
    public sealed class InventoryItemUI : MonoBehaviour
    {
        private readonly List<Vector2Int> _offsetBuffer = new(8);

        private InventoryItemInstance _item;
        private Image _icon;
        private RectTransform _rect;
        private LayoutElement _layoutElement;

        public InventoryItemInstance Item => _item;
        public RectTransform Rect => _rect;

        private void Awake()
        {
            _icon = GetComponent<Image>();
            _rect = GetComponent<RectTransform>();
            EnsureIgnoreLayout();
            if (_icon != null)
                _icon.raycastTarget = true;
        }

        public void SetItem(InventoryItemInstance inventoryItem)
        {
            _item = inventoryItem;
            if (_icon == null) _icon = GetComponent<Image>();
            if (_rect == null) _rect = GetComponent<RectTransform>();
            if (_icon == null) return;

            _icon.raycastTarget = true;
            // Fill the footprint (2×1 etc.) — preserveAspect would shrink a square icon to one cell.
            _icon.preserveAspect = false;
            _icon.type = Image.Type.Simple;
            if (_item?.definition?.icon != null)
            {
                _icon.sprite = _item.definition.icon;
                _icon.color = Color.white;
            }
            else
            {
                // Prefab supplies a UISprite; don't clear it or the footprint won't draw.
                _icon.color = new Color(0.25f, 0.45f, 0.85f, 0.95f);
            }
        }

        /// <summary>
        /// Size and pin to the occupied cell footprint so visuals match <see cref="ItemDefinition.shape"/>.
        /// </summary>
        public void ApplyLayout(InventoryGridUI grid, RectTransform itemLayer, int gridX, int gridY, int rotation)
        {
            if (_rect == null) _rect = GetComponent<RectTransform>();
            if (_item?.definition == null || grid == null || itemLayer == null) return;

            EnsureIgnoreLayout();

            // Never sit under GridLayoutGroup — it forces every child to 1×1 cell size.
            if (_rect.parent != itemLayer)
                _rect.SetParent(itemLayer, false);

            _rect.localScale = Vector3.one;
            _rect.localRotation = Quaternion.identity;
            _rect.pivot = new Vector2(0f, 1f);
            _rect.anchorMin = new Vector2(0f, 1f);
            _rect.anchorMax = new Vector2(0f, 1f);

            if (!TryGetFootprintLocal(grid, itemLayer, gridX, gridY, rotation, out var localPos, out var size))
                return;

            _rect.anchoredPosition = localPos;
            _rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x);
            _rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y);
        }

        public void ApplySize(InventoryGridUI grid, int rotation)
        {
            if (_rect == null) _rect = GetComponent<RectTransform>();
            if (_item?.definition == null || grid == null) return;

            EnsureIgnoreLayout();
            GetShapeBounds(rotation, out var boundW, out var boundH);

            var layout = grid.Layout;
            var cellSize = layout != null ? layout.cellSize : new Vector2(64f, 64f);
            var spacing = layout != null ? layout.spacing : Vector2.zero;
            var size = new Vector2(
                boundW * cellSize.x + Mathf.Max(0, boundW - 1) * spacing.x,
                boundH * cellSize.y + Mathf.Max(0, boundH - 1) * spacing.y);

            _rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x);
            _rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y);
        }

        private void EnsureIgnoreLayout()
        {
            if (_layoutElement == null)
                _layoutElement = GetComponent<LayoutElement>();
            if (_layoutElement == null)
                _layoutElement = gameObject.AddComponent<LayoutElement>();
            _layoutElement.ignoreLayout = true;
        }

        private void GetShapeBounds(int rotation, out int boundW, out int boundH)
        {
            var shape = _item.definition.shape;
            shape.GetOccupiedOffsets(rotation, _offsetBuffer);
            if (_offsetBuffer.Count == 0)
            {
                shape.GetBounds(rotation, out boundW, out boundH);
                boundW = Mathf.Max(1, boundW);
                boundH = Mathf.Max(1, boundH);
                return;
            }

            var minX = _offsetBuffer[0].x;
            var maxX = _offsetBuffer[0].x;
            var minY = _offsetBuffer[0].y;
            var maxY = _offsetBuffer[0].y;
            for (var i = 1; i < _offsetBuffer.Count; i++)
            {
                var o = _offsetBuffer[i];
                if (o.x < minX) minX = o.x;
                if (o.x > maxX) maxX = o.x;
                if (o.y < minY) minY = o.y;
                if (o.y > maxY) maxY = o.y;
            }

            boundW = Mathf.Max(1, maxX - minX + 1);
            boundH = Mathf.Max(1, maxY - minY + 1);
        }

        private bool TryGetFootprintLocal(
            InventoryGridUI grid,
            RectTransform itemLayer,
            int gridX,
            int gridY,
            int rotation,
            out Vector2 localPos,
            out Vector2 size)
        {
            localPos = Vector2.zero;
            size = Vector2.zero;
            if (gridX < 0 || gridY < 0) return false;

            GetShapeBounds(rotation, out var boundW, out var boundH);

            var start = grid.GetCell(gridX, gridY);
            var end = grid.GetCell(gridX + boundW - 1, gridY + boundH - 1);
            if (start?.Rect == null || end?.Rect == null) return false;

            var startCorners = new Vector3[4];
            var endCorners = new Vector3[4];
            start.Rect.GetWorldCorners(startCorners);
            end.Rect.GetWorldCorners(endCorners);

            // 1 = top-left, 3 = bottom-right
            var localTL = itemLayer.InverseTransformPoint(startCorners[1]);
            var localBR = itemLayer.InverseTransformPoint(endCorners[3]);

            localPos = new Vector2(localTL.x, localTL.y);
            size = new Vector2(Mathf.Abs(localBR.x - localTL.x), Mathf.Abs(localTL.y - localBR.y));
            return size.x > 0.5f && size.y > 0.5f;
        }
    }
}
