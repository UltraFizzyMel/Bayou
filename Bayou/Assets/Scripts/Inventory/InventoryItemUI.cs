using System.Collections.Generic;
using Bayou.Inventory.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Bayou.Inventory
{
    [RequireComponent(typeof(Image))]
    [RequireComponent(typeof(RectTransform))]
    public sealed class InventoryItemUI : MonoBehaviour
    {
        private static readonly Color PlateColor = new(0.78f, 0.68f, 0.48f, 0.92f);
        private static readonly Color PlateEmptyIcon = new(0.32f, 0.48f, 0.78f, 0.92f);

        private readonly List<Vector2Int> _offsetBuffer = new(8);

        private InventoryItemInstance _item;
        private Image _plate;
        private Image _icon;
        private RectTransform _rect;
        private LayoutElement _layoutElement;

        public InventoryItemInstance Item => _item;
        public RectTransform Rect => _rect;

        private void Awake()
        {
            _rect = GetComponent<RectTransform>();
            EnsureVisuals();
            EnsureIgnoreLayout();
        }

        public void SetItem(InventoryItemInstance inventoryItem)
        {
            _item = inventoryItem;
            if (_rect == null) _rect = GetComponent<RectTransform>();
            EnsureVisuals();
            ApplyIcon();
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
            ApplyIcon();
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
            ApplyIcon();
        }

        private void EnsureVisuals()
        {
            _plate = GetComponent<Image>();
            if (_plate != null)
            {
                _plate.sprite = UiWhiteSprite.Get();
                _plate.type = Image.Type.Simple;
                _plate.preserveAspect = false;
                _plate.raycastTarget = true;
            }

            var iconTf = transform.Find("Icon");
            if (iconTf != null)
                _icon = iconTf.GetComponent<Image>();

            if (_icon == null)
            {
                var go = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(transform, false);
                _icon = go.GetComponent<Image>();
            }

            var iconRt = _icon.rectTransform;
            iconRt.anchorMin = Vector2.zero;
            iconRt.anchorMax = Vector2.one;
            iconRt.pivot = new Vector2(0.5f, 0.5f);
            iconRt.offsetMin = new Vector2(6f, 6f);
            iconRt.offsetMax = new Vector2(-6f, -6f);
            iconRt.localScale = Vector3.one;
            iconRt.localRotation = Quaternion.identity;

            _icon.raycastTarget = false;
            _icon.type = Image.Type.Simple;
            _icon.preserveAspect = true;
        }

        private void ApplyIcon()
        {
            EnsureVisuals();
            if (_plate != null)
                _plate.color = _item?.definition?.icon != null ? PlateColor : PlateEmptyIcon;

            if (_icon == null) return;

            var sprite = _item?.definition?.icon;
            _icon.sprite = sprite;
            _icon.enabled = sprite != null;
            _icon.color = Color.white;
            _icon.type = Image.Type.Simple;
            _icon.preserveAspect = true;

            if (sprite == null)
                return;

            var fitter = _icon.GetComponent<AspectRatioFitter>();
            if (fitter == null)
                fitter = _icon.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            var h = sprite.rect.height;
            fitter.aspectRatio = h > 0.01f ? sprite.rect.width / h : 1f;
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
