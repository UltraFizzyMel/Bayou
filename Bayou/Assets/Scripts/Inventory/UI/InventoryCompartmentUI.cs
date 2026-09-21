using System.Collections.Generic;
using Bayou.Inventory;
using UnityEngine;
using UnityEngine.UI;

namespace Bayou.Inventory.UI
{
    /// <summary>
    /// One backpack pocket: cell grid + item layer, positioned over the art.
    /// </summary>
    public sealed class InventoryCompartmentUI
    {
        public string CompartmentId { get; }
        public RectTransform GridRoot { get; }
        public RectTransform ItemsRoot { get; }
        public InventoryGridModel Grid { get; }
        public float CellSize { get; }
        public float CellSpacing { get; }

        private Vector2 _contentOffset;
        private readonly List<Image> _cellImages = new();

        public InventoryCompartmentUI(
            string compartmentId,
            RectTransform gridRoot,
            RectTransform itemsRoot,
            InventoryGridModel grid,
            float cellSize,
            float cellSpacing)
        {
            CompartmentId = compartmentId;
            GridRoot = gridRoot;
            ItemsRoot = itemsRoot;
            Grid = grid;
            CellSize = cellSize;
            CellSpacing = cellSpacing;
        }

        public void RegisterCell(Image cellImage) => _cellImages.Add(cellImage);

        public IReadOnlyList<Image> CellImages => _cellImages;

        public void SetContentOffset(Vector2 offset) => _contentOffset = offset;

        public Vector2 GridToLocal(int x, int y)
        {
            var step = CellSize + CellSpacing;
            return _contentOffset + new Vector2(x * step, -y * step);
        }

        public Vector2 GridToAnchoredPosition(int anchorX, int anchorY, ItemShape shape, int rotation)
        {
            // Items use a top-left pivot (0,1). Position is the top-left of the
            // anchor cell — do not subtract height or rotation swaps Y.
            _ = shape;
            _ = rotation;
            return GridToLocal(anchorX, anchorY);
        }

        public Vector2 GetItemSize(ItemShape shape, int rotation)
        {
            shape.GetBounds(rotation, out var bw, out var bh);
            var step = CellSize + CellSpacing;
            return new Vector2(
                bw * step - CellSpacing,
                bh * step - CellSpacing);
        }

        public Vector2 GridPixelSize =>
            new(
                Grid.Width * (CellSize + CellSpacing) - CellSpacing,
                Grid.Height * (CellSize + CellSpacing) - CellSpacing);

        public bool ScreenPointToGrid(Vector2 screenPoint, Camera cam, out int gx, out int gy)
        {
            gx = gy = -1;
            if (GridRoot == null) return false;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    GridRoot, screenPoint, cam, out var local))
                return false;

            local -= _contentOffset;
            var step = Mathf.Max(0.01f, CellSize + CellSpacing);
            gx = Mathf.FloorToInt(local.x / step);
            gy = Mathf.FloorToInt(-local.y / step);

            if (gx >= 0 && gy >= 0 && gx < Grid.Width && gy < Grid.Height)
                return true;

            // Inside the grid rect (including cell gutters) — clamp to the nearest valid cell.
            var size = GridPixelSize;
            if (local.x < -CellSpacing || local.x > size.x + CellSpacing ||
                -local.y < -CellSpacing || -local.y > size.y + CellSpacing)
                return false;

            gx = Mathf.Clamp(gx, 0, Grid.Width - 1);
            gy = Mathf.Clamp(gy, 0, Grid.Height - 1);
            return true;
        }

        public void SnapItemToGrid(InventoryItemInstance item, int anchorX, int anchorY, RectTransform itemRect)
        {
            if (item?.definition == null || itemRect == null) return;
            InventoryItemView.PrepareGridRect(itemRect);
            itemRect.sizeDelta = GetItemSize(item.definition.shape, item.rotation);
            itemRect.anchoredPosition = GridToAnchoredPosition(anchorX, anchorY, item.definition.shape, item.rotation);
        }

        public void ResetCellColors(Color empty)
        {
            foreach (var img in _cellImages)
                if (img != null) img.color = empty;
        }
    }
}
