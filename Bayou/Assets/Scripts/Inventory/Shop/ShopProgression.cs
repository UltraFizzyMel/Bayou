using Bayou.Save;
using UnityEngine;

namespace Bayou.Inventory.Shop
{
    /// <summary>
    /// Extra Caliste stock. Each row appears after its world flag is set
    /// (finding her stall, or handing a cargo box back).
    /// </summary>
    public static class ShopProgression
    {
        private static readonly (string flag, string itemId)[] Unlocks =
        {
            (WorldFlags.ShopNetUpgrade, "Item_NetPatch"),
            (WorldFlags.ShopUpgradedRod, "Item_FishingRod"),
            (WorldFlags.ShopWadingBoots, "Item_Satchel"),
            (WorldFlags.ShopStraps, "Item_NetPatch"),
            (WorldFlags.ShopSatchel, "Item_Satchel"),
        };

        public static void Append(InventoryBagModel bag, InventoryController playerInventory)
        {
            if (bag == null) return;

            for (var i = 0; i < Unlocks.Length; i++)
            {
                var (flag, itemId) = Unlocks[i];
                if (!WorldFlags.Has(flag)) continue;
                if (BagAlreadyStocks(bag, itemId)) continue;

                var item = Resolve(itemId);
                if (item == null) continue;
                if (ShopDefinition.IsUniqueAlreadyOwned(item, playerInventory)) continue;
                if (!bag.TryAddItem(item, 0, out _))
                    Debug.LogWarning($"[Shop] Could not place unlock {item.displayName}.");
            }
        }

        private static bool BagAlreadyStocks(InventoryBagModel bag, string itemId)
        {
            foreach (var stocked in bag.AllItems)
            {
                if (stocked?.definition != null && stocked.definition.MatchesId(itemId))
                    return true;
            }

            return false;
        }

        private static ItemDefinition Resolve(string itemId)
        {
            var catalog = GameSaveSystem.Instance != null ? GameSaveSystem.Instance.ItemCatalog : null;
            catalog ??= Resources.Load<ItemCatalog>("Bayou/ItemCatalog");
            var fromCatalog = catalog != null ? catalog.Resolve(itemId) : null;
            if (fromCatalog != null) return fromCatalog;

            var fromResources = Resources.Load<ItemDefinition>($"Bayou/Items/{itemId}");
            if (fromResources != null) return fromResources;

            foreach (var def in Resources.LoadAll<ItemDefinition>("Bayou/Items"))
            {
                if (def != null && def.MatchesId(itemId))
                    return def;
            }

#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<ItemDefinition>(
                $"Assets/Inventory/Items/{itemId}.asset");
#else
            return null;
#endif
        }
    }
}
