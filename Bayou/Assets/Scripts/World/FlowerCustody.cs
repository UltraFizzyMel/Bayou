using Bayou.Inventory;
using Bayou.Inventory.Shop;
using UnityEngine;

namespace Bayou.World
{
    /// <summary>
    /// The marsh flower has to reach the grave. Discard and sale are refused
    /// while Search for a Herbalist or A Grave Revelation still needs it.
    /// </summary>
    public static class FlowerCustody
    {
        public const string ItemId = "Item_HerbalistFlower";
        public const string BlockedMessage = "It still has to be placed on the grave.";

        private static int _blockedFrame = -1;

        public static bool IsFlower(InventoryItemInstance item) =>
            item?.definition != null && item.definition.MatchesId(ItemId);

        public static bool WasBlockedThisFrame => _blockedFrame == Time.frameCount;

        public static bool QuestStillNeedsIt()
        {
            if (FlowerAlreadyPlaced()) return false;
            return QuestOpen(QuestIds.SearchForHerbalist) || QuestOpen(QuestIds.VisitGraves);
        }

        /// <summary>True when this flower must stay in the bag. Shows the grave line.</summary>
        public static bool BlockIfQuestNeedsIt(InventoryItemInstance item)
        {
            if (!IsFlower(item) || !QuestStillNeedsIt())
                return false;

            ShowBlocked();
            return true;
        }

        public static void ShowBlocked()
        {
            _blockedFrame = Time.frameCount;
            var shop = ShopUIController.ActiveShop;
            if (shop != null && shop.IsOpen)
                shop.ShowNotice(BlockedMessage);
            AccordionNotice.Show(BlockedMessage);
        }

        private static bool QuestOpen(string questId)
        {
            var quests = QuestManager.Resolve();
            if (quests == null || !quests.TryGetQuest(questId, out var quest) || quest == null)
                return false;
            return quest.state == QuestState.IN_PROGRESS || quest.state == QuestState.CAN_FINISH;
        }

        private static bool FlowerAlreadyPlaced()
        {
            var dialogue = DialogueManager.GetInstance();
            if (dialogue != null &&
                dialogue.TryGetBoolVariable("flowerPlaced", out var placed) &&
                placed)
                return true;

            var quests = QuestManager.Resolve();
            return quests != null &&
                   quests.TryGetQuest(QuestIds.VisitGraves, out var quest) &&
                   quest != null &&
                   quest.state == QuestState.FINISHED;
        }
    }
}
