using Bayou.Player;
using Bayou.Quests;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class VisitGravesQuestStep : QuestStep
{
    [SerializeField] private string markerLabel = "Grave";

    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent<BayouCharacterMotor>(out _) ||
            other.GetComponentInParent<BayouCharacterMotor>() != null)
        {
            FinishQuestStep();
        }
    }

    public override bool TryGetObjectiveWorldPosition(out Vector3 worldPosition, out string label)
    {
        worldPosition = transform.position + Vector3.up * 1.4f;
        label = markerLabel;
        return true;
    }

    protected override void SetQuestStepState(string state)
    {
        // no state needed
    }
}
