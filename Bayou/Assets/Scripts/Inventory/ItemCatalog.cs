using System.Collections.Generic;
using UnityEngine;

namespace Bayou.Inventory
{
    [CreateAssetMenu(menuName = "Bayou/Inventory/Item Catalog", fileName = "ItemCatalog")]
    public sealed class ItemCatalog : ScriptableObject
    {
        [SerializeField] private ItemDefinition[] items = System.Array.Empty<ItemDefinition>();

        private Dictionary<string, ItemDefinition> _lookup;

        public void BuildLookup()
        {
            _lookup = new Dictionary<string, ItemDefinition>();
            if (items == null) return;

            foreach (var item in items)
            {
                if (item == null) continue;
                _lookup[item.name] = item;
            }
        }

        public ItemDefinition Resolve(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId)) return null;
            _lookup ??= new Dictionary<string, ItemDefinition>();
            if (_lookup.Count == 0 && items != null && items.Length > 0)
                BuildLookup();
            return _lookup.TryGetValue(itemId, out var def) ? def : null;
        }

        public ItemDefinition[] AllDefinitions => items ?? System.Array.Empty<ItemDefinition>();

#if UNITY_EDITOR
        public void SetItems(ItemDefinition[] catalogItems) => items = catalogItems;
#endif
    }
}
