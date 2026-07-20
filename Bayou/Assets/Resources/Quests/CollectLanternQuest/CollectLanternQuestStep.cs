using Bayou.Inventory;
using UnityEngine;

/// <summary>
/// Completes when the player picks up the lantern (demo endpiece).
/// </summary>
public sealed class CollectLanternQuestStep : QuestStep
{
    [SerializeField] private string lanternItemId = "Item_Lantern";

    private InventoryController _inv;
    private bool _subscribed;

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
        if (!inv.HasItemsById(lanternItemId, 1)) return;
        FinishQuestStep();
    }

    private static InventoryController ResolveInventory() =>
        InventoryController.Instance ?? Object.FindFirstObjectByType<InventoryController>();

    protected override void SetQuestStepState(string state) => CheckProgress();
}
