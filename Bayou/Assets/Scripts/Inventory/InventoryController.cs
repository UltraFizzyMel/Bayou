using System;
using System.Collections.Generic;
using UnityEngine;

namespace Bayou.Inventory
{
    [DisallowMultipleComponent]
    public sealed class InventoryController : MonoBehaviour
    {
        public static InventoryController Instance { get; private set; }

        [Header("Layout")]
        [SerializeField] private BackpackLayoutDefinition backpackLayout;

        [Header("Legacy single grid (only if Backpack Layout is empty)")]
        [SerializeField] private int gridWidth = 7;
        [SerializeField] private int gridHeight = 6;

        [Header("Upgrades (shelved)")]
        [Tooltip("When off, every compartment in the layout is usable. Re-enable when backpack pocket upgrades ship.")]
        [SerializeField] private bool enableCompartmentUpgrades;
        [SerializeField] private List<BackpackUpgradeDefinition> startingUpgrades = new();

        [Header("Starter items (optional)")]
        [SerializeField] private List<ItemDefinition> startingItems = new();

        private readonly HashSet<string> _unlockedCompartments = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _appliedUpgradeIds = new(StringComparer.OrdinalIgnoreCase);

        public InventoryBagModel Bag { get; private set; }
        public BackpackLayoutDefinition Layout => backpackLayout;
        public bool CompartmentUpgradesEnabled => enableCompartmentUpgrades;

        public InventoryGridModel Grid =>
            Bag != null && Bag.CompartmentIds.Count > 0
                ? Bag.GetGrid(Bag.CompartmentIds[0])
                : null;

        public event Action InventoryChanged;
        public event Action<string> CompartmentUnlocked;

        private void Awake()
        {
            if (Instance != null && Instance != this)
                Debug.LogWarning("[Inventory] Multiple InventoryController instances; replacing singleton.");
            Instance = this;

            Bag = backpackLayout != null
                ? InventoryBagModel.FromLayout(backpackLayout)
                : InventoryBagModel.Single(gridWidth, gridHeight);

            InitializeUnlocks();

            if (enableCompartmentUpgrades)
            {
                foreach (var upgrade in startingUpgrades)
                {
                    if (upgrade != null)
                        ApplyUpgrade(upgrade, silent: true);
                }
            }

            foreach (var def in startingItems)
            {
                if (def != null)
                    TryAddItem(def);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void InitializeUnlocks()
        {
            _unlockedCompartments.Clear();
            _appliedUpgradeIds.Clear();

            if (backpackLayout?.compartments == null)
            {
                if (Bag != null)
                {
                    foreach (var id in Bag.CompartmentIds)
                        _unlockedCompartments.Add(id);
                }
                return;
            }

            foreach (var c in backpackLayout.compartments)
            {
                if (c == null || string.IsNullOrWhiteSpace(c.id)) continue;
                if (c.unlockedAtStart || string.IsNullOrWhiteSpace(c.requiredUpgradeId))
                    _unlockedCompartments.Add(c.id);
            }
        }

        public bool IsCompartmentUnlocked(string compartmentId)
        {
            if (string.IsNullOrWhiteSpace(compartmentId)) return false;
            if (!enableCompartmentUpgrades || backpackLayout == null)
                return Bag != null && Bag.TryGetGrid(compartmentId, out _);
            if (_unlockedCompartments.Contains(compartmentId))
                return true;

            var cfg = FindCompartmentConfig(compartmentId);
            return cfg != null
                   && !string.IsNullOrWhiteSpace(cfg.requiredUpgradeId)
                   && HasUpgrade(cfg.requiredUpgradeId);
        }

        private InventoryCompartmentConfig FindCompartmentConfig(string compartmentId)
        {
            if (backpackLayout?.compartments == null) return null;
            foreach (var c in backpackLayout.compartments)
            {
                if (c != null && string.Equals(c.id, compartmentId, StringComparison.OrdinalIgnoreCase))
                    return c;
            }
            return null;
        }

        public bool HasUpgrade(string upgradeId) =>
            !string.IsNullOrWhiteSpace(upgradeId) && _appliedUpgradeIds.Contains(upgradeId);

        /// <summary>Unlock a pocket from a <see cref="BackpackUpgradeDefinition"/> (shop, story, etc.).</summary>
        public bool ApplyUpgrade(BackpackUpgradeDefinition upgrade, bool silent = false)
        {
            if (!enableCompartmentUpgrades) return false;
            if (upgrade == null || string.IsNullOrWhiteSpace(upgrade.upgradeId)) return false;
            if (_appliedUpgradeIds.Contains(upgrade.upgradeId)) return false;

            _appliedUpgradeIds.Add(upgrade.upgradeId);

            if (!string.IsNullOrWhiteSpace(upgrade.unlocksCompartmentId))
                UnlockCompartment(upgrade.unlocksCompartmentId, silent);

            UnlockCompartmentsRequiringUpgrade(upgrade.upgradeId, silent);

            if (!silent)
                NotifyChanged();
            return true;
        }

        private void UnlockCompartmentsRequiringUpgrade(string upgradeId, bool silent)
        {
            if (backpackLayout?.compartments == null || string.IsNullOrWhiteSpace(upgradeId)) return;

            foreach (var c in backpackLayout.compartments)
            {
                if (c == null || string.IsNullOrWhiteSpace(c.id)) continue;
                if (!string.Equals(c.requiredUpgradeId, upgradeId, StringComparison.OrdinalIgnoreCase))
                    continue;
                UnlockCompartment(c.id, silent);
            }
        }

        public void UnlockCompartment(string compartmentId, bool silent = false)
        {
            if (string.IsNullOrWhiteSpace(compartmentId)) return;
            if (!_unlockedCompartments.Add(compartmentId)) return;

            if (!silent)
            {
                CompartmentUnlocked?.Invoke(compartmentId);
                NotifyChanged();
            }
        }

        public bool TryAddItem(ItemDefinition definition, int rotation = 0)
        {
            if (definition == null || Bag == null) return false;
            if (Bag.TryAddItem(definition, rotation, out _, IsCompartmentUnlocked))
            {
                NotifyChanged();
                return true;
            }
            return false;
        }

        public bool TryPlace(InventoryItemInstance item, string compartmentId, int x, int y, int rotation)
        {
            if (Bag == null || item == null) return false;
            if (!IsCompartmentUnlocked(compartmentId)) return false;
            if (!Bag.TryPlace(item, compartmentId, x, y, rotation)) return false;
            NotifyChanged();
            return true;
        }

        public bool TryPlace(InventoryItemInstance item, int x, int y, int rotation)
        {
            if (item != null && item.IsPlaced)
                return TryPlace(item, item.compartmentId, x, y, rotation);

            if (Bag == null) return false;
            foreach (var id in Bag.CompartmentIds)
            {
                if (!IsCompartmentUnlocked(id)) continue;
                if (TryPlace(item, id, x, y, rotation))
                    return true;
            }
            return false;
        }

        public void RemoveItem(InventoryItemInstance item)
        {
            if (Bag == null || item == null) return;
            Bag.Remove(item);
            NotifyChanged();
        }

        public void DetachForDrag(InventoryItemInstance item)
        {
            if (Bag == null || item == null) return;
            Bag.DetachFromGrid(item);
            NotifyChanged();
        }

        public void NotifyChanged() => InventoryChanged?.Invoke();

        public void ClearAllItems()
        {
            if (Bag == null) return;
            var copy = new List<InventoryItemInstance>(Bag.AllItems);
            foreach (var item in copy)
                Bag.Remove(item);
            NotifyChanged();
        }

        public List<InventoryItemInstance> GetFishItems()
        {
            var result = new List<InventoryItemInstance>();
            if (Bag == null) return result;

            foreach (var item in Bag.AllItems)
            {
                if (item?.definition != null && item.definition.isFish)
                    result.Add(item);
            }

            return result;
        }
    }
}
