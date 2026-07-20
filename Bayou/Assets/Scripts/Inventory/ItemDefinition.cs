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

        /// <summary>Canonical id for matching (gates, Ink, inventory counts).</summary>
        public string Id => string.IsNullOrWhiteSpace(itemId) ? name : itemId;

        public bool MatchesId(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return false;
            return string.Equals(Id, id, System.StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(name, id, System.StringComparison.OrdinalIgnoreCase);
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
