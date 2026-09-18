using Bayou.Player;
using UnityEngine;

/// <summary>
/// Completes when the player enters this trigger, or comes within radius of a named scene object.
/// </summary>
[RequireComponent(typeof(Collider))]
public sealed class VisitNamedLocationQuestStep : QuestStep
{
    [SerializeField] private string locationObjectName;
    [SerializeField] private float proximityRadius = 3.5f;
    [SerializeField] private string markerLabel = "Objective";

    private void Reset()
    {
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    private void Update()
    {
        if (string.IsNullOrWhiteSpace(locationObjectName)) return;
        var target = GameObject.Find(locationObjectName);
        if (target == null) return;

        var player = PlayerLocator.Transform;
        if (player == null) return;

        var delta = target.transform.position - player.position;
        delta.y = 0f;
        if (delta.sqrMagnitude <= proximityRadius * proximityRadius)
            FinishQuestStep();
    }

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
        label = markerLabel;
        if (!string.IsNullOrWhiteSpace(locationObjectName))
        {
            var target = GameObject.Find(locationObjectName);
            if (target != null)
            {
                worldPosition = target.transform.position + Vector3.up * 1.4f;
                return true;
            }
        }

        worldPosition = transform.position + Vector3.up * 1.4f;
        return true;
    }

    protected override void SetQuestStepState(string state)
    {
        if (!string.IsNullOrWhiteSpace(state))
            markerLabel = state;
    }
}
