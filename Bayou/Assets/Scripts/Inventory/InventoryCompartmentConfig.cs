using System;
using UnityEngine;

namespace Bayou.Inventory
{
    public enum InventoryCompartmentAnchor
    {
        [Tooltip("Offset from panel top-left (classic backpack pockets).")]
        TopLeft = 0,

        [Tooltip("Offset from panel bottom-right (RE attaché case grid).")]
        BottomRight = 1
    }

    /// <summary>
    /// One storage pocket on the panel (backpack pockets or attaché-case grid).
    /// </summary>
    [Serializable]
    public sealed class InventoryCompartmentConfig
    {
        public string id = "top";
        public string displayName = "Top";

        public int gridWidth = 4;
        public int gridHeight = 4;

        [Tooltip("Which panel corner this pocket is pinned to.")]
        public InventoryCompartmentAnchor anchor = InventoryCompartmentAnchor.TopLeft;

        [Tooltip("TopLeft: offset right/down from top-left. BottomRight: offset left/up from bottom-right.")]
        public Vector2 anchoredPosition = new(24f, -48f);

        [Tooltip("Pixel size of the slot area on the panel (ignored when Fill Panel is on).")]
        public Vector2 slotAreaSize = new(200f, 200f);

        [Tooltip("Stretch this compartment to fill the inventory panel (minus UI padding). Best for a single 7×6 case.")]
        public bool fillPanel = true;

        [Header("Upgrades (shelved)")]
        [Tooltip("Ignored while InventoryController → Enable Compartment Upgrades is off.")]
        public bool unlockedAtStart = true;

        [Tooltip("Ignored while compartment upgrades are off.")]
        public string requiredUpgradeId;
    }
}
