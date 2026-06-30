using System.Collections.Generic;
using UnityEngine;

namespace Bayou.Inventory
{
    /// <summary>
    /// Resident Evil–style puzzle grid: items occupy multiple cells by shape + rotation.
    /// </summary>
    public sealed class InventoryGridModel
    {
        private readonly int _width;
        private readonly int _height;
        private readonly InventoryItemInstance[,] _cells;
        private readonly List<InventoryItemInstance> _items = new();
        private readonly List<Vector2Int> _offsetBuffer = new();

        public int Width => _width;
        public int Height => _height;
        public IReadOnlyList<InventoryItemInstance> Items => _items;

        public InventoryGridModel(int width, int height)
        {
            _width = Mathf.Max(1, width);
            _height = Mathf.Max(1, height);
            _cells = new InventoryItemInstance[_width, _height];
        }

        public bool CanPlace(InventoryItemInstance item, int anchorX, int anchorY, int rotation)
        {
            if (item?.definition == null) return false;
            item.definition.shape.GetOccupiedOffsets(rotation, _offsetBuffer);
            foreach (var o in _offsetBuffer)
            {
                var x = anchorX + o.x;
                var y = anchorY + o.y;
                if (x < 0 || y < 0 || x >= _width || y >= _height)
                    return false;
                var occupant = _cells[x, y];
                if (occupant != null && occupant != item)
                    return false;
            }
            return true;
        }

        public bool TryPlace(InventoryItemInstance item, int anchorX, int anchorY, int rotation)
        {
            if (!CanPlace(item, anchorX, anchorY, rotation))
                return false;

            if (item.IsPlaced)
                Remove(item);

            item.gridX = anchorX;
            item.gridY = anchorY;
            item.rotation = ((rotation % 4) + 4) % 4;

            if (!_items.Contains(item))
                _items.Add(item);

            WriteCells(item);
            return true;
        }

        public void Remove(InventoryItemInstance item)
        {
            if (item == null) return;
            DetachFromGrid(item);
            _items.Remove(item);
        }

        /// <summary>Clears grid cells but keeps the instance in the bag (used while dragging).</summary>
        public void DetachFromGrid(InventoryItemInstance item)
        {
            if (item == null) return;
            ClearCells(item);
            item.gridX = -1;
            item.gridY = -1;
        }

        public InventoryItemInstance GetItemAt(int x, int y)
        {
            if (x < 0 || y < 0 || x >= _width || y >= _height) return null;
            return _cells[x, y];
        }

        public bool TryAddNewItem(ItemDefinition definition, int rotation, out InventoryItemInstance instance)
        {
            instance = new InventoryItemInstance(definition, rotation);
            if (TryFindFirstFit(instance, out var ax, out var ay))
            {
                TryPlace(instance, ax, ay, instance.rotation);
                return true;
            }
            return false;
        }

        public bool TryFindFirstFit(InventoryItemInstance item, out int anchorX, out int anchorY)
        {
            for (var r = 0; r < 4; r++)
            {
                for (var y = 0; y < _height; y++)
                {
                    for (var x = 0; x < _width; x++)
                    {
                        if (CanPlace(item, x, y, r))
                        {
                            anchorX = x;
                            anchorY = y;
                            item.rotation = r;
                            return true;
                        }
                    }
                }
            }
            anchorX = -1;
            anchorY = -1;
            return false;
        }

        private void WriteCells(InventoryItemInstance item)
        {
            item.definition.shape.GetOccupiedOffsets(item.rotation, _offsetBuffer);
            foreach (var o in _offsetBuffer)
            {
                var x = item.gridX + o.x;
                var y = item.gridY + o.y;
                _cells[x, y] = item;
            }
        }

        private void ClearCells(InventoryItemInstance item)
        {
            if (!item.IsPlaced || item.definition == null) return;
            item.definition.shape.GetOccupiedOffsets(item.rotation, _offsetBuffer);
            foreach (var o in _offsetBuffer)
            {
                var x = item.gridX + o.x;
                var y = item.gridY + o.y;
                if (x >= 0 && y >= 0 && x < _width && y < _height && _cells[x, y] == item)
                    _cells[x, y] = null;
            }
        }
    }
}
