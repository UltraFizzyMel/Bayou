using Bayou.Inventory;
using UnityEngine;

/// <summary>
/// Advances when the player holds both a Red Snapper and a Sailfin Molly.
/// </summary>
public sealed class SnapperAndMollyQuestStep : QuestStep
{
    [SerializeField] private string snapperItemId = "Item_RedSnapper";
    [SerializeField] private string mollyItemId = "Item_SailfinMolly";

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

        var inv = ResolveInventory();
        var needSnapper = inv == null || !inv.HasItemsById(snapperItemId, 1);
        var needMolly = inv == null || !inv.HasItemsById(mollyItemId, 1);

        // Point at whichever fish is still missing (prefer nearest missing type).
        if (needSnapper || needMolly)
        {
            if (QuestObjectiveLocator.TryFindNearestNeededFish(near, out worldPosition, out label))
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

        var hasSnapper = inv.HasItemsById(snapperItemId, 1);
        var hasMolly = inv.HasItemsById(mollyItemId, 1);
        if (hasSnapper && hasMolly)
        {
            FinishQuestStep();
            return;
        }

        var state = $"{(hasSnapper ? 1 : 0)}/1 snapper, {(hasMolly ? 1 : 0)}/1 molly";
        if (state == _lastState) return;
        _lastState = state;
        try { ChangeState(state); }
        catch (System.Exception) { }
    }

    private static InventoryController ResolveInventory() =>
        InventoryController.Instance ?? Object.FindFirstObjectByType<InventoryController>();

    protected override void SetQuestStepState(string state) => CheckProgress();
}
