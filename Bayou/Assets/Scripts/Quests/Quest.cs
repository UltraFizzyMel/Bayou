using UnityEngine;

public class Quest
{
    //staticInfo
    public QuestInfoSO info;

    //state info
    public QuestState state;
    private int currentQuestStepIndex;
    private QuestStepState[] questStepStates;

    public Quest(QuestInfoSO questInfo )
    {
        this.info = questInfo;
        this.state = QuestState.REQUIREMENTS_NOT_MET;
        this.currentQuestStepIndex = 0;
        this.questStepStates = new QuestStepState[info.questStepPrefabs.Length];
        for( int i = 0; i< questStepStates.Length; i++ )
        {
            questStepStates[i] = new QuestStepState();
        }
    }

    public Quest(QuestInfoSO questInfo, QuestState questState, int currentQuestStepIndex, QuestStepState[] questStepStates)
    {
        this.info = questInfo;
        this.state = questState;
        this.currentQuestStepIndex = currentQuestStepIndex;
        this.questStepStates = questStepStates;

        //if the quest step states and prefabs are different lengths
        //something has changed during development and the svaed data is out of sync
        if(this.questStepStates.Length != this.info.questStepPrefabs.Length )
        {
            Debug.LogWarning("Quest Step Prefabs and Quest Step States are of different lengths. "
                + "This indicates something changed with the QuestInfo and the saved data is now out of sync."
                + "Reset your data - as this might cause issues. QuestId" + this.info.id);
        }
    }

    public int CurrentQuestStepIndex => currentQuestStepIndex;

    public bool IsActiveForHud =>
        state == QuestState.IN_PROGRESS || state == QuestState.CAN_FINISH;

    /// <summary>Progress text for the current (or last) step — used by the gameplay quest log.</summary>
    public string GetHudObjectiveText()
    {
        if (questStepStates == null || questStepStates.Length == 0)
            return state == QuestState.CAN_FINISH ? "Ready to turn in" : "";

        var index = Mathf.Clamp(currentQuestStepIndex, 0, questStepStates.Length - 1);
        if (state == QuestState.CAN_FINISH)
            return "Ready to turn in";

        var step = questStepStates[index];
        return step != null && !string.IsNullOrWhiteSpace(step.state) ? step.state : "In progress";
    }

    public void MoveToNextStep()
    {
        currentQuestStepIndex++;
    }

    public void ReturnToPreviousStep()
    {
        currentQuestStepIndex--;
    }

    public bool CurrentStepExists()
    { 
        return(currentQuestStepIndex < info.questStepPrefabs.Length);
    }

    public void InstantiateCurrentQuestStep(Transform parentTransform)
    {
        GameObject questStepPrefab = GetCurrentQuestStepPrefab();
        if (questStepPrefab != null) 
        { 
           QuestStep questStep = Object.Instantiate<GameObject>(questStepPrefab, parentTransform).GetComponent<QuestStep>();
            questStep.InitializeQuestStep(info.id, currentQuestStepIndex, questStepStates[currentQuestStepIndex].state);
        }
    }

    private GameObject GetCurrentQuestStepPrefab()
    {
        GameObject questStepPrefab = null;
        if (CurrentStepExists()) 
        { questStepPrefab = info.questStepPrefabs[currentQuestStepIndex]; }
        else 
        { 
            Debug.LogWarning("Tried to get quest step prefab, but stepIndex was out of range indicating that " + 
            "there's no current step: " + info.id + ", stepIndex=" + currentQuestStepIndex); 
        }

        return questStepPrefab;
    }

    public void StoreQuestStateData(QuestStepState questStepState, int stepIndex )
    {
        if (stepIndex < questStepStates.Length)
        {
            questStepStates[stepIndex].state = questStepState.state;
        }
        else
        {
            Debug.LogWarning("Tried to access quest step data, but StepIndex was out of range: "
                + "Quest Id = " + info.id + ", Step Index = " + stepIndex);
        }
    }

    public QuestData GetQuestData() 
    { 
        return new QuestData(state, currentQuestStepIndex, questStepStates);
    }

}

 
