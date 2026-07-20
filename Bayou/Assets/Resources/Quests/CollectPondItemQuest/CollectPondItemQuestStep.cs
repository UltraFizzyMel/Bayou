using Bayou.Inventory;
using UnityEngine;

/// <summary>
/// Completes (FinishQuestStep → AdvanceQuest → CAN_FINISH) as soon as the player has the required item.
/// </summary>
public sealed class CollectPondItemQuestStep : QuestStep
{
    [SerializeField] private ItemDefinition requiredItem;
    [SerializeField] private string requiredItemId = "Item_ShinyPond";
    [SerializeField] private int requiredCount = 1;

    private InventoryController _inv;
    private bool _subscribed;
    private string _lastState;

    private void OnEnable()
    {
        TrySubscribe();
        CheckProgress();
    }

    private void OnDisable() => Unsubscribe();

    private void Update()
    {
        // Backup: Instance may appear after this step spawns, or an event may be missed.
        if (!_subscribed)
            TrySubscribe();

        CheckProgress();
    }

    private void TrySubscribe()
    {
        var inv = ResolveInventory();
        if (inv == null || _subscribed) return;

        _inv = inv;
        _inv.InventoryChanged += OnInventoryChanged;
        _subscribed = true;
    }

    private void Unsubscribe()
    {
        if (_inv != null && _subscribed)
            _inv.InventoryChanged -= OnInventoryChanged;
        _subscribed = false;
        _inv = null;
    }

    private void OnInventoryChanged() => CheckProgress();

    private void CheckProgress()
    {
        var inv = ResolveInventory();
        if (inv == null) return;

        var need = Mathf.Max(1, requiredCount);
        var have = CountOwned(inv);

        if (have >= need)
        {
            // Finish first — this advances the quest to CAN_FINISH.
            FinishQuestStep();
            return;
        }

        var state = $"{have}/{need}";
        if (state == _lastState) return;
        _lastState = state;

        // Optional UI/debug state; never block finishing.
        try
        {
            ChangeState(state);
        }
        catch (System.Exception)
        {
            // Ignore if quest events aren't ready yet.
        }
    }

    private int CountOwned(InventoryController inv)
    {
        if (requiredItem != null)
        {
            var byRef = inv.CountItems(requiredItem);
            if (byRef > 0) return byRef;
        }

        var byId = inv.CountItemsById(requiredItemId);
        if (byId > 0) return byId;

        // Fallback: match name containing "Shiny" (asset rename / missing id).
        if (inv.Bag == null) return 0;
        var count = 0;
        foreach (var item in inv.Bag.AllItems)
        {
            var def = item?.definition;
            if (def == null) continue;
            if (def.name.IndexOf("Shiny", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                def.displayName.IndexOf("Shiny", System.StringComparison.OrdinalIgnoreCase) >= 0)
                count++;
        }

        return count;
    }

    private static InventoryController ResolveInventory()
    {
        if (InventoryController.Instance != null)
            return InventoryController.Instance;
        return Object.FindFirstObjectByType<InventoryController>();
    }

    protected override void SetQuestStepState(string state) => CheckProgress();
}
