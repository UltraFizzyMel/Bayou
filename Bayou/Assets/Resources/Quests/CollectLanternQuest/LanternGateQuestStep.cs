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

    private string _authoredLabel;
    private string _postedLabel;

    private void Awake()
    {
        _authoredLabel = string.IsNullOrWhiteSpace(markerLabel) ? "Gate" : markerLabel;
    }

    private void Start()
    {
        if (string.IsNullOrWhiteSpace(_authoredLabel))
            _authoredLabel = string.IsNullOrWhiteSpace(markerLabel) ? "Gate" : markerLabel;

        if (ShouldFinish())
        {
            FinishQuestStep();
            return;
        }

        RefreshObjective();
    }

    private void Update()
    {
        if (ShouldFinish())
        {
            FinishQuestStep();
            return;
        }

        RefreshObjective();
    }

    public override bool TryGetObjectiveWorldPosition(out Vector3 worldPosition, out string label)
    {
        if (WantsCaliste() && QuestObjectiveLocator.TryFindCaliste(out worldPosition, out label))
        {
            label = "Caliste";
            return true;
        }

        label = string.IsNullOrWhiteSpace(_authoredLabel) ? markerLabel : _authoredLabel;
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
        if (string.IsNullOrWhiteSpace(state)) return;
        if (string.Equals(state, "Caliste", System.StringComparison.Ordinal)) return;
        markerLabel = state;
        _authoredLabel = state;
    }

    private void RefreshObjective()
    {
        var next = WantsCaliste() ? "Caliste" : _authoredLabel;
        if (string.IsNullOrWhiteSpace(next) || next == _postedLabel) return;
        _postedLabel = next;
        ChangeState(next);
    }

    private static bool WantsCaliste()
    {
        if (PlayerHasMazeKey()) return false;
        return QuestObjectiveLocator.TryFindCaliste(out _, out _);
    }

    private static bool PlayerHasMazeKey()
    {
        var inv = InventoryController.Instance ?? Object.FindFirstObjectByType<InventoryController>();
        if (inv != null && inv.HasItemsById(KeyGateManager.MazeGateKeyItemId, 1))
            return true;
        var keys = KeyGateManager.Instance ?? Object.FindFirstObjectByType<KeyGateManager>();
        return keys != null && keys.hasKeyMazeGate;
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
            if (string.Equals(keyFlag, KeyGateManager.GraveyardKeyFlag, System.StringComparison.OrdinalIgnoreCase) &&
                gate.HierarchyContains("Foggy"))
                continue;
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
