using Bayou.Inventory;
using Bayou.Inventory.Shop;

namespace Bayou.UI
{
    /// <summary>Journal, inventory, and shop should not stack on top of each other.</summary>
    public static class UiModalExclusion
    {
        public static void OnJournalOpening()
        {
            InventoryDisplayUI.Active?.ForceClose();
            ActiveShop?.CloseShop();
        }

        public static void OnInventoryOpening()
        {
            QuestJournalHud.Instance?.Close();
            ActiveShop?.CloseShop();
        }

        public static void OnShopOpening()
        {
            QuestJournalHud.Instance?.Close();
            InventoryDisplayUI.Active?.ForceClose();
        }

        public static bool BlocksJournal()
        {
            if (InventoryDisplayUI.Active != null && InventoryDisplayUI.Active.IsOpen) return true;
            if (ActiveShop != null && ActiveShop.IsOpen) return true;
            return false;
        }

        public static bool BlocksInventory()
        {
            if (QuestJournalHud.IsOpen) return true;
            if (ActiveShop != null && ActiveShop.IsOpen) return true;
            return false;
        }

        public static bool BlocksShop()
        {
            if (QuestJournalHud.IsOpen) return true;
            if (InventoryDisplayUI.Active != null && InventoryDisplayUI.Active.IsOpen) return true;
            return false;
        }

        private static ShopUIController ActiveShop => ShopUIController.ActiveShop;
    }
}