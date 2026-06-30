using System;
using UnityEngine;

namespace Bayou.Inventory.Shop
{
    [Serializable]
    public sealed class ShopStockEntry
    {
        public ItemDefinition item;
        [Range(0, 3)] public int rotation;
        [Tooltip("Leave empty to auto-place in the shop grid.")]
        public string compartmentId = "case";
        public int gridX = -1;
        public int gridY = -1;
    }

    [CreateAssetMenu(menuName = "Bayou/Shop/Shop Definition", fileName = "Shop_")]
    public sealed class ShopDefinition : ScriptableObject
    {
        public string merchantName = "Shopkeeper";
        public BackpackLayoutDefinition layout;

        [Tooltip("Items for sale. Auto-placed if grid position is unset.")]
        public ShopStockEntry[] stock = Array.Empty<ShopStockEntry>();

        public InventoryBagModel CreateStockBag()
        {
            var bag = layout != null
                ? InventoryBagModel.FromLayout(layout)
                : InventoryBagModel.Single(7, 6, "case");

            foreach (var entry in stock)
            {
                if (entry?.item == null) continue;

                var instance = new InventoryItemInstance(entry.item, entry.rotation);
                if (entry.gridX >= 0 && entry.gridY >= 0 &&
                    bag.TryPlace(instance, ResolveCompartment(bag, entry.compartmentId), entry.gridX, entry.gridY, instance.rotation))
                    continue;

                if (!bag.TryAddItem(entry.item, entry.rotation, out _))
                    Debug.LogWarning($"[Shop] Could not place {entry.item.displayName} in merchant stock.");
            }

            return bag;
        }

        private static string ResolveCompartment(InventoryBagModel bag, string preferred)
        {
            if (!string.IsNullOrWhiteSpace(preferred) && bag.TryGetGrid(preferred, out _))
                return preferred;
            return bag.CompartmentIds.Count > 0 ? bag.CompartmentIds[0] : "case";
        }
    }
}
