using System;
using UnityEngine;

namespace Bayou.Inventory
{
    [Serializable]
    public sealed class InventoryItemInstance
    {
        public string instanceId;
        public ItemDefinition definition;
        public int rotation; // 0–3
        public string compartmentId;
        public int gridX = -1;
        public int gridY = -1;
        public int stackCount = 1;

        public bool IsPlaced =>
            !string.IsNullOrEmpty(compartmentId) && gridX >= 0 && gridY >= 0;

        public InventoryItemInstance(ItemDefinition def, int rot = 0)
        {
            instanceId = Guid.NewGuid().ToString("N");
            definition = def;
            rotation = ((rot % 4) + 4) % 4;
            stackCount = 1;
        }
    }
}
