using UnityEngine;

namespace Bayou.Inventory
{
    /// <summary>Shelved — enable <see cref="InventoryController.CompartmentUpgradesEnabled"/> before using.</summary>
    [CreateAssetMenu(menuName = "Bayou/Inventory/Backpack Upgrade (shelved)", fileName = "Upgrade_")]
    public sealed class BackpackUpgradeDefinition : ScriptableObject    {
        [Tooltip("Unique id saved in player progress / used by compartment config.")]
        public string upgradeId = "upgrade_middle";

        public string displayName = "Expand middle pocket";

        [Tooltip("Compartment id from Backpack Layout (e.g. middle, top).")]
        public string unlocksCompartmentId = "middle";
    }
}
