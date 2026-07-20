using Bayou.Inventory;
using Bayou.Inventory.Shop;
using Bayou.Save;
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
        story.BindExternalFunction("GiveItem", (string itemId, int count) => GiveItem(itemId, count));
        story.BindExternalFunction("GrantKey", (string flagName) => GrantKey(flagName));
        story.BindExternalFunction("OpenShop", () => OpenShop());
    }

    public void Unbind(Story story)
    {
        story.UnbindExternalFunction("StartQuest");
        story.UnbindExternalFunction("AdvanceQuest");
        story.UnbindExternalFunction("FinishQuest");
        story.UnbindExternalFunction("HasItem");
        story.UnbindExternalFunction("HandOverItem");
        story.UnbindExternalFunction("GiveItem");
        story.UnbindExternalFunction("GrantKey");
        story.UnbindExternalFunction("OpenShop");
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

    private static bool GiveItem(string itemId, int count)
    {
        count = Mathf.Max(1, count);
        var inv = InventoryController.Instance;
        if (inv == null)
        {
            Debug.LogWarning("[Ink] GiveItem: no InventoryController.");
            return false;
        }

        var def = ResolveItem(itemId);
        if (def == null)
        {
            Debug.LogWarning($"[Ink] GiveItem: unknown item '{itemId}'.");
            return false;
        }

        var given = 0;
        for (var i = 0; i < count; i++)
        {
            if (inv.TryAddItem(def) || inv.TryHoldNewItem(def, out _))
                given++;
        }

        if (given < count)
            Debug.LogWarning($"[Ink] GiveItem: only added {given}/{count} of {itemId} (bag full?).");

        return given > 0;
    }

    private static void GrantKey(string flagName)
    {
        var mgr = KeyGateManager.Instance ?? UnityEngine.Object.FindFirstObjectByType<KeyGateManager>();
        if (mgr == null)
        {
            Debug.LogWarning("[Ink] GrantKey: no KeyGateManager in the scene.");
            return;
        }

        mgr.GrantKeyFlag(flagName);
        Debug.Log($"[Ink] Granted key flag '{flagName}'.");
    }

    private static ItemDefinition ResolveItem(string itemId)
    {
        var catalog = GameSaveSystem.Instance != null ? GameSaveSystem.Instance.ItemCatalog : null;
        if (catalog != null)
        {
            var fromCatalog = catalog.Resolve(itemId);
            if (fromCatalog != null) return fromCatalog;
        }

#if UNITY_EDITOR
        return UnityEditor.AssetDatabase.LoadAssetAtPath<ItemDefinition>(
            $"Assets/Inventory/Items/{itemId}.asset");
#else
        return null;
#endif
    }

    /// <summary>Closes dialogue and opens Caliste's shop UI (deferred one frame so Ink can finish).</summary>
    private static void OpenShop()
    {
        var dialogue = DialogueManager.GetInstance();
        if (dialogue != null)
        {
            dialogue.QueueOpenShop();
            return;
        }

        OpenShopImmediate();
    }

    internal static void OpenShopImmediate()
    {
        var keeper = UnityEngine.Object.FindFirstObjectByType<Shopkeeper>();
        var shopDef = keeper != null ? keeper.ShopDefinition : null;

        var shopUi = ShopUiBuilder.EnsureInScene(shopDef) ?? UnityEngine.Object.FindFirstObjectByType<ShopUIController>();
        if (shopUi == null)
        {
            Debug.LogWarning("[Ink] OpenShop: no ShopUIController in the scene.");
            return;
        }

        var handmade = InventoryDisplayUI.Active ?? UnityEngine.Object.FindFirstObjectByType<InventoryDisplayUI>();
        if (handmade != null)
            shopUi.AssignHandmadeInventory(handmade);

        shopUi.OpenShop(shopDef);
    }
}
