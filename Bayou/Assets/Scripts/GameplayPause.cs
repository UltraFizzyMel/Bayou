using Bayou.Inventory;
using Bayou.Inventory.Shop;
using Bayou.Inventory.UI;
using Bayou.Save;
using UnityEngine;

namespace Bayou
{
    /// <summary>
    /// Pauses gameplay while any menu UI (inventory, shop, bonfire) is open.
    /// </summary>
    public static class GameplayPause
    {
        private static float _savedTimeScale = 1f;

        public static bool IsPaused => Time.timeScale <= 0f;

        /// <summary>Inventory, shop, catch reveal, and other menus own the interact key.</summary>
        public static bool BlocksWorldInteract =>
            IsPaused ||
            IsInventoryOpen() ||
            CaughtFishPresenter.IsBusy;

        public static bool IsDialoguePlaying
        {
            get
            {
                var dialogue = DialogueManager.GetInstance();
                return dialogue != null && dialogue.dialogueIsPlaying;
            }
        }

        public static void CloseInventoryMenus()
        {
            if (InventoryDisplayUI.Active != null && InventoryDisplayUI.Active.IsOpen)
                InventoryDisplayUI.Active.Close();

            var procedural = Object.FindFirstObjectByType<InventoryUIController>();
            if (procedural != null && procedural.IsOpen)
                procedural.Close();
        }

        public static void SyncFromUiState()
        {
            var shouldPause =
                IsInventoryOpen() ||
                CaughtFishPresenter.IsBusy ||
                AudioSettings.IsOpen ||
                Bayou.UI.QuestJournalHud.IsOpen ||
                (ShopUIController.ActiveShop != null && ShopUIController.ActiveShop.IsOpen) ||
                (BonfireUIController.Active != null && BonfireUIController.Active.IsOpen);

            if (shouldPause)
            {
                if (Time.timeScale > 0f)
                {
                    _savedTimeScale = Time.timeScale;
                    Time.timeScale = 0f;
                }
            }
            else if (Time.timeScale <= 0f)
            {
                Time.timeScale = _savedTimeScale > 0f ? _savedTimeScale : 1f;
            }
        }

        private static bool IsInventoryOpen()
        {
            if (InventoryDisplayUI.Active != null && InventoryDisplayUI.Active.IsOpen)
                return true;

            var ui = Object.FindFirstObjectByType<InventoryUIController>();
            return ui != null && ui.IsOpen;
        }

    }
}
