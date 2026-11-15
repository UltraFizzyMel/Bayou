using Bayou.Inventory;
using Bayou.Inventory.Shop;
using UnityEngine;

namespace Bayou.World
{
    /// <summary>
    /// One guard for selling or discarding Olivier's accordion.
    /// While Lost Accordion is in progress the item stays in the bag.
    /// A completed sale or discard sets accordion_disposed / accordian_disposed.
    /// Turning the quest in does not count as disposal.
    /// </summary>
    public static class AccordionCustody
    {
        public const string ItemId = "Item_Accordion";
        public const string BlockedMessage = "Olivier is looking for this.";

        private static int _blockedFrame = -1;

        public static bool IsAccordion(InventoryItemInstance item) =>
            item?.definition != null && item.definition.MatchesId(ItemId);

        public static bool LostQuestInProgress()
        {
            var quests = QuestManager.Resolve();
            return quests != null &&
                   quests.TryGetQuest(QuestIds.LostAccordion, out var quest) &&
                   quest != null &&
                   quest.state == QuestState.IN_PROGRESS;
        }

        public static bool WasBlockedThisFrame => _blockedFrame == Time.frameCount;

        /// <summary>True when this accordion must stay in the bag. Shows the Olivier line.</summary>
        public static bool BlockIfQuestNeedsIt(InventoryItemInstance item)
        {
            if (!IsAccordion(item) || !LostQuestInProgress())
                return false;

            _blockedFrame = Time.frameCount;
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
            Debug.Log("[Accordion] " + BlockedMessage);
        }

        public static void MarkDisposed()
        {
            if (LostQuestInProgress())
                return;

            WorldFlags.Set(WorldFlags.DisposedAccordion, true);
            var dialogue = DialogueManager.GetInstance();
            dialogue?.SetBoolVariable("accordian_disposed", true);
            dialogue?.SetBoolVariable("accordion_disposed", true);
        }

        public static void MarkReturned()
        {
            WorldFlags.Set(WorldFlags.ReturnedAccordion, true);
            var dialogue = DialogueManager.GetInstance();
            dialogue?.SetBoolVariable("accordian_returned", true);
            dialogue?.SetBoolVariable("accordion_returned", true);
        }

        public static void NoteMerchantBag(InventoryBagModel merchantBag)
        {
            if (merchantBag == null || LostQuestInProgress())
                return;

            foreach (var item in merchantBag.AllItems)
            {
                if (!IsAccordion(item)) continue;
                MarkDisposed();
                return;
            }
        }
    }

    /// <summary>Short on-screen line when the accordion cannot leave the bag.</summary>
    public sealed class AccordionNotice : MonoBehaviour
    {
        private static AccordionNotice _instance;
        private string _message;
        private float _until;

        public static void Show(string message)
        {
            if (_instance == null)
            {
                var go = new GameObject(nameof(AccordionNotice));
                _instance = go.AddComponent<AccordionNotice>();
            }

            _instance._message = message;
            _instance._until = Time.unscaledTime + 3.2f;
        }

        private void OnGUI()
        {
            if (Time.unscaledTime > _until || string.IsNullOrEmpty(_message))
                return;

            var style = new GUIStyle(GUI.skin.box)
            {
                fontSize = 18,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
            style.normal.textColor = new Color(0.98f, 0.94f, 0.82f);
            var width = 520f;
            var rect = new Rect((Screen.width - width) * 0.5f, Screen.height * 0.72f, width, 64f);
            GUI.Box(rect, _message, style);
        }
    }
}
