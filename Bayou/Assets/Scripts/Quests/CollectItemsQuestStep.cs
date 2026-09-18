using System;
using Bayou.Inventory;
using UnityEngine;

/// <summary>Advances when the player holds every listed item/count.</summary>
public sealed class CollectItemsQuestStep : QuestStep
{
    [Serializable]
    public sealed class Need
    {
        public string itemId;
        public int count = 1;
        public string label;
    }

    [SerializeField] private Need[] needs = Array.Empty<Need>();

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
        if (needs != null)
        {
            for (var i = 0; i < needs.Length; i++)
            {
                var need = needs[i];
                if (need == null || string.IsNullOrWhiteSpace(need.itemId)) continue;
                var want = Mathf.Max(1, need.count);
                var have = inv != null ? inv.CountItemsById(need.itemId) : 0;
                if (have >= want) continue;

                if (QuestMarkerTarget.TryFind(QuestId, turnIn: false, need.itemId, near, out var beacon))
                {
                    worldPosition = beacon.MarkerWorldPosition;
                    label = beacon.Label;
                    return true;
                }

                if (QuestObjectiveLocator.TryFindPickupByItemId(need.itemId, near, out worldPosition, out label))
                    return true;
            }
        }

        return QuestObjectiveLocator.TryFindNearestNeededFish(near, out worldPosition, out label);
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
        if (inv == null || needs == null || needs.Length == 0) return;

        var complete = true;
        var parts = new string[needs.Length];
        for (var i = 0; i < needs.Length; i++)
        {
            var need = needs[i];
            if (need == null || string.IsNullOrWhiteSpace(need.itemId))
            {
                parts[i] = "";
                continue;
            }

            var want = Mathf.Max(1, need.count);
            var have = Mathf.Min(want, inv.CountItemsById(need.itemId));
            var noun = string.IsNullOrWhiteSpace(need.label) ? need.itemId : need.label;
            parts[i] = $"{have}/{want} {noun}";
            if (have < want) complete = false;
        }

        if (complete)
        {
            FinishQuestStep();
            return;
        }

        var state = string.Join(", ", parts);
        if (state == _lastState) return;
        _lastState = state;
        try { ChangeState(state); }
        catch (Exception) { }
    }

    private static InventoryController ResolveInventory() =>
        InventoryController.Instance ?? FindFirstObjectByType<InventoryController>();

    protected override void SetQuestStepState(string state) => CheckProgress();
}
