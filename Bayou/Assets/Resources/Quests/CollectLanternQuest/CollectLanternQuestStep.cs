using Bayou.Inventory;
using Bayou.Quests;
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

    public override bool TryGetObjectiveWorldPosition(out Vector3 worldPosition, out string label)
    {
        var near = transform.position;
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) near = player.transform.position;

        if (QuestMarkerTarget.TryFind(QuestId, turnIn: false, lanternItemId, near, out var beacon))
        {
            worldPosition = beacon.MarkerWorldPosition;
            label = beacon.Label;
            return true;
        }

        if (QuestObjectiveLocator.TryFindPickupByItemId(lanternItemId, near, out worldPosition, out label))
            return true;

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
        if (!inv.HasItemsById(lanternItemId, 1)) return;
        FinishQuestStep();
    }

    private static InventoryController ResolveInventory() =>
        InventoryController.Instance ?? Object.FindFirstObjectByType<InventoryController>();

    protected override void SetQuestStepState(string state) => CheckProgress();
}
