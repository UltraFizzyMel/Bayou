using Bayou.Inventory;
using Ink.Runtime;
using UnityEngine;

public class InkExternalFunctions
{
    public void Bind(Story story)
    {
        story.BindExternalFunction("StartQuest", (string questId) => StartQuest(questId));
        story.BindExternalFunction("AdvanceQuest", (string questId) => AdvanceQuest(questId));
        story.BindExternalFunction("FinishQuest", (string questId) => FinishQuest(questId));
        story.BindExternalFunction("HasItem", (string itemId, int count) => HasItem(itemId, count));
        story.BindExternalFunction("HandOverItem", (string itemId, int count) => HandOverItem(itemId, count));
    }

    public void Unbind(Story story)
    {
        story.UnbindExternalFunction("StartQuest");
        story.UnbindExternalFunction("AdvanceQuest");
        story.UnbindExternalFunction("FinishQuest");
        story.UnbindExternalFunction("HasItem");
        story.UnbindExternalFunction("HandOverItem");
    }

    private void StartQuest(string questId)
    {
        GameEventManager.Instance.questEvents.StartQuest(questId);
    }

    private void AdvanceQuest(string questId)
    {
        GameEventManager.Instance.questEvents.AdvanceQuest(questId);
    }

    private void FinishQuest(string questId)
    {
        GameEventManager.Instance.questEvents.FinishQuest(questId);
    }

    private static bool HasItem(string itemId, int count)
    {
        var inv = InventoryController.Instance;
        if (inv == null) return false;
        return inv.HasItemsById(itemId, Mathf.Max(1, count));
    }

    private static bool HandOverItem(string itemId, int count)
    {
        var inv = InventoryController.Instance;
        if (inv == null) return false;
        return inv.TryRemoveItemsById(itemId, Mathf.Max(1, count));
    }
}
