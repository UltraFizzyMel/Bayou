using UnityEngine;

namespace Bayou.Inventory.Shop
{
    /// <summary>
    /// Shared MockUI palette — shop matches the handmade inventory case.
    /// </summary>
    public static class ShopUiStyle
    {
        public static readonly Color PanelBrown = new(0.45f, 0.34f, 0.22f, 1f);
        public static readonly Color CellCream = new(0.92f, 0.88f, 0.78f, 1f);
        public static readonly Color OverlayDim = new(0.12f, 0.08f, 0.05f, 0.55f);
        public static readonly Color HeaderFooter = new(0.32f, 0.24f, 0.16f, 0.96f);
        public static readonly Color TextCream = new(0.96f, 0.92f, 0.84f, 1f);
        public static readonly Color ButtonGreen = new(0.28f, 0.42f, 0.26f, 1f);
        public static readonly Color ButtonMuted = new(0.38f, 0.30f, 0.22f, 1f);
        public static readonly Color HoverValid = new(0.2f, 0.55f, 0.28f, 0.85f);
        public static readonly Color HoverInvalid = new(0.55f, 0.18f, 0.14f, 0.9f);
    }
}
