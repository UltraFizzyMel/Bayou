using System.Collections.Generic;
using UnityEngine;

namespace Bayou.Inventory
{
    /// <summary>
    /// Multiple compartment grids (top / middle / bottom backpack pockets). Items cannot span compartments.
    /// </summary>
    public sealed class InventoryBagModel
    {
        private readonly Dictionary<string, InventoryGridModel> _grids = new();
        private readonly List<InventoryItemInstance> _allItems = new();
        private readonly List<string> _compartmentOrder = new();

        public IReadOnlyList<string> CompartmentIds => _compartmentOrder;
        public IReadOnlyList<InventoryItemInstance> AllItems => _allItems;

        public static InventoryBagModel FromLayout(BackpackLayoutDefinition layout)
        {
            var bag = new InventoryBagModel();
            if (layout?.compartments == null) return bag;

            foreach (var c in layout.compartments)
            {
                if (c == null || string.IsNullOrWhiteSpace(c.id)) continue;
                bag.AddCompartment(c.id, c.gridWidth, c.gridHeight);
            }

            return bag;
        }

        public static InventoryBagModel Single(int width, int height, string id = "main")
        {
            var bag = new InventoryBagModel();
            bag.AddCompartment(id, width, height);
            return bag;
        }

        public void AddCompartment(string id, int width, int height)
        {
            if (_grids.ContainsKey(id)) return;
            _grids[id] = new InventoryGridModel(width, height);
            _compartmentOrder.Add(id);
        }

        public bool TryGetGrid(string compartmentId, out InventoryGridModel grid)
        {
            if (string.IsNullOrWhiteSpace(compartmentId))
            {
                grid = null;
                return false;
            }
            return _grids.TryGetValue(compartmentId, out grid);
        }

        public InventoryGridModel GetGrid(string compartmentId) =>
            _grids.TryGetValue(compartmentId, out var g) ? g : null;

        public bool TryAddItem(
            ItemDefinition definition,
            int rotation,
            out InventoryItemInstance instance,
            System.Func<string, bool> compartmentFilter = null)
        {
            instance = new InventoryItemInstance(definition, rotation);
            foreach (var id in _compartmentOrder)
            {
                if (compartmentFilter != null && !compartmentFilter(id))
                    continue;

                var grid = _grids[id];
                if (grid.TryFindFirstFit(instance, out var ax, out var ay))
                {
                    TryPlace(instance, id, ax, ay, instance.rotation);
                    return true;
                }
            }
            return false;
        }

        public bool CanPlace(InventoryItemInstance item, string compartmentId, int x, int y, int rotation)
        {
            return TryGetGrid(compartmentId, out var grid) && grid.CanPlace(item, x, y, rotation);
        }

        public bool TryPlace(InventoryItemInstance item, string compartmentId, int x, int y, int rotation)
        {
            if (!TryGetGrid(compartmentId, out var grid)) return false;
            if (!grid.CanPlace(item, x, y, rotation)) return false;

            if (item.IsPlaced && item.compartmentId != compartmentId)
                DetachFromGrid(item);

            if (item.IsPlaced && item.compartmentId == compartmentId)
                grid.DetachFromGrid(item);

            item.compartmentId = compartmentId;
            if (!grid.TryPlace(item, x, y, rotation)) return false;

            if (!_allItems.Contains(item))
                _allItems.Add(item);
            return true;
        }

        public void DetachFromGrid(InventoryItemInstance item)
        {
            if (item == null) return;
            if (item.IsPlaced && TryGetGrid(item.compartmentId, out var grid))
                grid.DetachFromGrid(item);
            item.compartmentId = null;
            item.gridX = -1;
            item.gridY = -1;
        }

        public void Remove(InventoryItemInstance item)
        {
            if (item == null) return;
            if (item.IsPlaced && TryGetGrid(item.compartmentId, out var grid))
                grid.Remove(item);
            _allItems.Remove(item);
        }

        /// <summary>
        /// Keeps an item in the bag without a grid cell (catch allocation, shop revert, failed drop).
        /// </summary>
        public void HoldItem(InventoryItemInstance item)
        {
            if (item == null) return;
            DetachFromGrid(item);
            if (!_allItems.Contains(item))
                _allItems.Add(item);
        }

        public bool TryFindFirstFitAnywhere(
            InventoryItemInstance item,
            out string compartmentId,
            out int x,
            out int y,
            System.Func<string, bool> compartmentFilter = null)
        {
            foreach (var id in _compartmentOrder)
            {
                if (compartmentFilter != null && !compartmentFilter(id))
                    continue;

                var grid = _grids[id];
                if (grid.TryFindFirstFit(item, out x, out y))
                {
                    compartmentId = id;
                    return true;
                }
            }
            compartmentId = null;
            x = y = -1;
            return false;
        }
    }
}
