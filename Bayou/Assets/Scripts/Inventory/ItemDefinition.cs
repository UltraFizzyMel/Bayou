using UnityEngine;

namespace Bayou.Inventory
{
    [CreateAssetMenu(menuName = "Bayou/Inventory/Item Definition", fileName = "Item_")]
    public sealed class ItemDefinition : ScriptableObject
    {
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
    }
}
