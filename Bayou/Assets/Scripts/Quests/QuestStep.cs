using UnityEngine;

public abstract class QuestStep : MonoBehaviour
{
    private bool isFinished = false;
    private string questId;
    private int stepIndex;

    public string QuestId => questId;
    public int StepIndex => stepIndex;
    public bool IsFinished => isFinished;

    public void InitializeQuestStep(string questId, int stepIndex, string questStepState)
    {
        this.questId = questId;
        this.stepIndex = stepIndex;
        if (questStepState != null && questStepState != "")
        {
            SetQuestStepState(questStepState);
        }
    }

    /// <summary>
    /// World position for the quest marker. Override per step type.
    /// Default: this transform (works for visit-location steps with authored positions).
    /// </summary>
    public virtual bool TryGetObjectiveWorldPosition(out Vector3 worldPosition, out string label)
    {
        worldPosition = transform.position + Vector3.up * 1.2f;
        label = string.IsNullOrWhiteSpace(name) ? "Objective" : name;
        return true;
    }

    protected void FinishQuestStep()
    {
        if (isFinished) return;
        isFinished = true;

        GameEventManager.Instance?.questEvents?.AdvanceQuest(questId);
        Destroy(this.gameObject);
    }

    protected void ChangeState(string newState)
    {
        GameEventManager.Instance?.questEvents?.QuestStepStateChange(
            questId, stepIndex, new QuestStepState(newState));
    }

    protected abstract void SetQuestStepState(string state);
}
