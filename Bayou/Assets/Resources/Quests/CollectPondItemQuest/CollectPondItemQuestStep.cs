using Bayou.Inventory;
using Bayou.Quests;
using UnityEngine;

/// <summary>
/// Completes (FinishQuestStep → AdvanceQuest → CAN_FINISH) as soon as the player has the required item.
/// </summary>
public sealed class CollectPondItemQuestStep : QuestStep
{
    [SerializeField] private ItemDefinition requiredItem;
    [SerializeField] private string requiredItemId = "Item_RosaryNecklace";
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
        if (!_subscribed)
            TrySubscribe();

        CheckProgress();
    }

    public override bool TryGetObjectiveWorldPosition(out Vector3 worldPosition, out string label)
    {
        var near = transform.position;
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) near = player.transform.position;

        if (QuestMarkerTarget.TryFind(QuestId, turnIn: false, requiredItemId, near, out var beacon))
        {
            worldPosition = beacon.MarkerWorldPosition;
            label = beacon.Label;
            return true;
        }

        var shiny = Object.FindFirstObjectByType<PondShinyCollectible>();
        if (shiny != null)
        {
            worldPosition = shiny.transform.position + Vector3.up * 0.8f;
            label = "Rosary in the pond";
            return true;
        }

        worldPosition = default;
        label = null;
        return false;
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
            FinishQuestStep();
            return;
        }

        var state = $"{have}/{need}";
        if (state == _lastState) return;
        _lastState = state;

        try { ChangeState(state); }
        catch (System.Exception) { }
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

        if (inv.Bag == null) return 0;
        var count = 0;
        foreach (var item in inv.Bag.AllItems)
        {
            var def = item?.definition;
            if (def == null) continue;
            if (ItemDefinition.IsPondQuestItem(def.Id) ||
                ItemDefinition.IsPondQuestItem(def.name) ||
                (def.displayName != null &&
                 (def.displayName.IndexOf("Rosary", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                  def.displayName.IndexOf("Shiny", System.StringComparison.OrdinalIgnoreCase) >= 0)))
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
