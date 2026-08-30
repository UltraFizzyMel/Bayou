using UnityEngine;

namespace Bayou.Inventory
{
    [CreateAssetMenu(menuName = "Bayou/Inventory/Item Definition", fileName = "Item_")]
    public sealed class ItemDefinition : ScriptableObject
    {
        [Tooltip("Stable id used by gates, Ink, and saves. Defaults to the asset name when empty.")]
        public string itemId;

        public string displayName = "Item";
        [TextArea(2, 4)] public string description;
        [Tooltip("Drag a sprite. Shown in the bag and on the hotwheel when this item is collected.")]
        public Sprite icon;
        public ItemShape shape;
        public int maxStack = 1;

        [Header("Shop")]
        [Tooltip("Price the player pays when buying from a merchant. 0 = not sold by merchants.")]
        public int buyPrice;

        [Tooltip("Price the player receives when selling to a merchant. 0 = merchant won't buy.")]
        public int sellPrice;

        [Header("Bonfire")]
        [Tooltip("Fish caught in the bayou can be cooked at a bonfire to save the game.")]
        public bool isFish;

        [Header("Equipment")]
        [Tooltip("Unique gear (rod, lantern, net). The shop refuses a second copy if the player already owns one.")]
        public bool isEquipment;

        /// <summary>Canonical id for matching (gates, Ink, inventory counts).</summary>
        public string Id => string.IsNullOrWhiteSpace(itemId) ? name : itemId;

        public bool MatchesId(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return false;
            if (string.Equals(Id, id, System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, id, System.StringComparison.OrdinalIgnoreCase))
                return true;

            // Legacy pond pickup id — the church-pond item is the rosary.
            return IsPondQuestItem(Id) && IsPondQuestItem(id);
        }

        public bool IsKeyItem
        {
            get
            {
                var id = Id;
                return !string.IsNullOrWhiteSpace(id) &&
                       id.IndexOf("Key", System.StringComparison.OrdinalIgnoreCase) >= 0;
            }
        }

        /// <summary>Fish, keys, quest pickups — the shop allows more than one.</summary>
        public bool IsShopCollectible =>
            isFish || IsKeyItem || IsPondQuestItem(Id);

        /// <summary>Rod / lantern / net — one per player in the shop.</summary>
        public bool IsUniqueEquipment
        {
            get
            {
                if (isEquipment) return true;
                if (IsShopCollectible) return false;
                var id = Id;
                return id.IndexOf("FishingRod", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                       id.IndexOf("Lantern", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                       id.IndexOf("NetPatch", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                       id.IndexOf("HandNet", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                       string.Equals(id, "Item_Net", System.StringComparison.OrdinalIgnoreCase);
            }
        }

        public static bool IsPondQuestItem(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return false;
            return string.Equals(id, "Item_RosaryNecklace", System.StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(id, "Item_ShinyPond", System.StringComparison.OrdinalIgnoreCase) ||
                   id.IndexOf("Rosary", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   id.IndexOf("ShinyPond", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void OnEnable()
        {
            if (string.IsNullOrWhiteSpace(itemId))
                itemId = name;
            var s = shape;
            s.EnsureValid();
            shape = s;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(itemId))
                itemId = name;
            var s = shape;
            s.EnsureValid();
            shape = s;
        }
#endif
    }
}
