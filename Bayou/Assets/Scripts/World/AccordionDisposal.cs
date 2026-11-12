using Bayou.Inventory;
using Bayou.Player;
using Bayou.UI;
using UnityEngine;

namespace Bayou.World
{
    /// <summary>
    /// Marie hands over Olivier's accordion and asks the player to get rid of it.
    /// Swimming with it and pressing interact sinks it. That sets the Ink flag her turn-in checks.
    /// </summary>
    public sealed class AccordionDisposal : MonoBehaviour, IInteractionPromptSource
    {
        private const string AccordionId = "Item_Accordion";

        public static void Ensure()
        {
            if (Object.FindFirstObjectByType<AccordionDisposal>() != null) return;
            var go = new GameObject(nameof(AccordionDisposal));
            go.AddComponent<AccordionDisposal>();
        }

        private void OnEnable() => InteractionPromptBroker.Register(this);

        private void OnDisable() => InteractionPromptBroker.Unregister(this);

        private void Update()
        {
            if (!CanDispose()) return;
            var input = InputManager.GetInstance();
            if (input == null || !input.GetInteractPressed()) return;
            Dispose();
        }

        public bool TryGetInteractionPrompt(out InteractionPrompt prompt)
        {
            prompt = default;
            if (!CanDispose()) return false;
            prompt = new InteractionPrompt("E", "Throw the accordion into the water", 40);
            return true;
        }

        private static bool CanDispose()
        {
            var quests = QuestManager.Resolve();
            if (quests == null || !quests.TryGetQuest(QuestIds.AccordionDisposal, out var quest))
                return false;
            if (quest.state != QuestState.IN_PROGRESS && quest.state != QuestState.CAN_FINISH)
                return false;

            var inv = InventoryController.Instance;
            if (inv == null || !inv.HasItemsById(AccordionId, 1))
                return false;

            var water = Object.FindFirstObjectByType<BayouWaterSensor>();
            return water != null && water.IsSwimming;
        }

        private static void Dispose()
        {
            var inv = InventoryController.Instance;
            if (inv == null || !inv.TryRemoveItemsById(AccordionId, 1))
                return;

            WorldFlags.Set(WorldFlags.DisposedAccordion, true);
            DialogueManager.GetInstance()?.SetBoolVariable("accordian_disposed", true);
            Debug.Log("[Accordion] Disposed in the bayou.");
        }
    }
}
