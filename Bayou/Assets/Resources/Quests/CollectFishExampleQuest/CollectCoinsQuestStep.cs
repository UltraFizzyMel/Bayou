using Bayou.Inventory;
using UnityEngine;

/// <summary>
/// Example collect step: completes when the player has enough fish in the bag.
/// </summary>
public sealed class CollectCoinsQuestStep : QuestStep
{
    [SerializeField] private int fishToComplete = 1;

    private void OnEnable()
    {
        var inv = InventoryController.Instance;
        if (inv != null)
            inv.InventoryChanged += OnInventoryChanged;
        CheckProgress();
    }

    private void OnDisable()
    {
        var inv = InventoryController.Instance;
        if (inv != null)
            inv.InventoryChanged -= OnInventoryChanged;
    }

    private void OnInventoryChanged() => CheckProgress();

    private void CheckProgress()
    {
        var inv = InventoryController.Instance;
        if (inv == null) return;

        var have = inv.GetFishItems().Count;
        ChangeState($"{have}/{Mathf.Max(1, fishToComplete)}");
        if (have >= Mathf.Max(1, fishToComplete))
            FinishQuestStep();
    }

    protected override void SetQuestStepState(string state) => CheckProgress();
}
