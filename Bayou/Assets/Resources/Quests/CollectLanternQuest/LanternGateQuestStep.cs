using Bayou.Inventory;
using UnityEngine;

/// <summary>
/// CollectLantern step: points at a gate until that gate is opened, then advances.
/// </summary>
public sealed class LanternGateQuestStep : QuestStep
{
    [SerializeField] private string keyFlag = "";
    [SerializeField] private string nameContains = "";
    [SerializeField] private string markerLabel = "Gate";
    [SerializeField] private string lanternItemId = "Item_Lantern";

    private void Start()
    {
        if (ShouldFinish())
        {
            FinishQuestStep();
            return;
        }

        ChangeState(markerLabel);
    }

    private void Update()
    {
        if (ShouldFinish())
            FinishQuestStep();
    }

    public override bool TryGetObjectiveWorldPosition(out Vector3 worldPosition, out string label)
    {
        label = markerLabel;
        var gate = FindGate();
        if (gate == null)
        {
            worldPosition = default;
            return false;
        }

        worldPosition = gate.transform.position + Vector3.up * 1.8f;
        return true;
    }

    protected override void SetQuestStepState(string state)
    {
        if (!string.IsNullOrWhiteSpace(state))
            markerLabel = state;
    }

    private bool ShouldFinish()
    {
        if (PlayerHasLantern()) return true;
        var gate = FindGate();
        return gate != null && gate.HasBeenOpened;
    }

    private InteractTrigger FindGate()
    {
        InteractTrigger fallback = null;
        var gates = InteractTrigger.AllGates();
        for (var i = 0; i < gates.Length; i++)
        {
            var gate = gates[i];
            if (gate == null) continue;
            if (!string.IsNullOrWhiteSpace(keyFlag) &&
                !string.Equals(gate.KeyFlagName, keyFlag, System.StringComparison.OrdinalIgnoreCase))
                continue;
            if (!string.IsNullOrWhiteSpace(nameContains) && !gate.HierarchyContains(nameContains))
                continue;
            if (string.IsNullOrWhiteSpace(keyFlag) && string.IsNullOrWhiteSpace(nameContains))
                continue;

            if (gate.HierarchyContains("Church"))
                return gate;
            fallback ??= gate;
        }

        return fallback;
    }

    private bool PlayerHasLantern()
    {
        var inv = InventoryController.Instance ?? Object.FindFirstObjectByType<InventoryController>();
        return inv != null && inv.HasItemsById(lanternItemId, 1);
    }
}
