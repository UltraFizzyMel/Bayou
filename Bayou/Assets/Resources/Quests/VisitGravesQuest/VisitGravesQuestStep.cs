using Bayou.Player;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class VisitGravesQuestStep : QuestStep
{
    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent<BayouCharacterMotor>(out BayouCharacterMotor bayouCharacterMotor))
        {
            FinishQuestStep();
        }
        
    }

    protected override void SetQuestStepState(string state)
    {
        //no state is needed for this quest step.
    }
}
