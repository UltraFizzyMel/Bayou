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
        var stepCount = info.questStepPrefabs != null ? info.questStepPrefabs.Length : 0;
        this.questStepStates = new QuestStepState[stepCount];
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
        this.questStepStates = questStepStates ?? new QuestStepState[0];

        var prefabCount = this.info != null && this.info.questStepPrefabs != null
            ? this.info.questStepPrefabs.Length
            : 0;
        if (this.questStepStates.Length != prefabCount)
        {
            Debug.LogWarning("Quest Step Prefabs and Quest Step States are of different lengths. "
                + "Saved progress will be rebuilt so the current step still advances. QuestId: "
                + (this.info != null ? this.info.id : "?"));
        }

        RepairLoadedProgress();
    }

    /// <summary>
    /// Old saves can have fewer steps than the current quest, or a missing step prefab.
    /// Resize the state array and point an in-progress quest at a step that still exists.
    /// </summary>
    public void RepairLoadedProgress()
    {
        var prefabs = info != null ? info.questStepPrefabs : null;
        var count = prefabs != null ? prefabs.Length : 0;

        if (questStepStates == null)
            questStepStates = new QuestStepState[0];

        if (questStepStates.Length != count)
        {
            var next = new QuestStepState[count];
            var keep = Mathf.Min(count, questStepStates.Length);
            for (var i = 0; i < count; i++)
            {
                next[i] = i < keep && questStepStates[i] != null
                    ? questStepStates[i]
                    : new QuestStepState();
            }

            questStepStates = next;
        }
        else
        {
            for (var i = 0; i < count; i++)
            {
                if (questStepStates[i] == null)
                    questStepStates[i] = new QuestStepState();
            }
        }

        if (state != QuestState.IN_PROGRESS)
            return;

        if (count == 0)
        {
            state = QuestState.CAN_FINISH;
            return;
        }

        if (currentQuestStepIndex < 0 || currentQuestStepIndex >= count)
            currentQuestStepIndex = count - 1;

        if (prefabs[currentQuestStepIndex] != null)
            return;

        for (var i = currentQuestStepIndex; i < count; i++)
        {
            if (prefabs[i] == null) continue;
            currentQuestStepIndex = i;
            return;
        }

        for (var i = 0; i < currentQuestStepIndex; i++)
        {
            if (prefabs[i] == null) continue;
            currentQuestStepIndex = i;
            return;
        }

        state = QuestState.CAN_FINISH;
    }

    public void PrepareForTurnIn()
    {
        var count = info != null && info.questStepPrefabs != null ? info.questStepPrefabs.Length : 0;
        currentQuestStepIndex = count;
        state = QuestState.CAN_FINISH;
    }

    public int CurrentQuestStepIndex => currentQuestStepIndex;

    public bool IsActiveForHud =>
        state == QuestState.IN_PROGRESS || state == QuestState.CAN_FINISH;

    public string GetJournalStatusLabel()
    {
        return state switch
        {
            QuestState.IN_PROGRESS => "Active",
            QuestState.CAN_FINISH => "Ready to turn in",
            QuestState.CAN_START => "Available",
            QuestState.FINISHED => "Completed",
            _ => "Locked"
        };
    }

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
        var steps = info != null ? info.questStepPrefabs : null;
        return steps != null && currentQuestStepIndex < steps.Length;
    }

    public void InstantiateCurrentQuestStep(Transform parentTransform)
    {
        if (!CurrentStepExists())
            return;

        GameObject questStepPrefab = GetCurrentQuestStepPrefab();
        if (questStepPrefab == null)
            return;

        // Instantiate in world space first so authored prefab positions (graves, etc.) stay put,
        // then parent under QuestManager for lifecycle without shifting the target.
        var go = Object.Instantiate(questStepPrefab);
        go.transform.SetParent(parentTransform, worldPositionStays: true);
        var questStep = go.GetComponent<QuestStep>();
        if (questStep == null)
            return;

        if (questStepStates == null || currentQuestStepIndex < 0 || currentQuestStepIndex >= questStepStates.Length)
            return;

        var stepState = questStepStates[currentQuestStepIndex] ?? new QuestStepState();
        questStepStates[currentQuestStepIndex] = stepState;
        questStep.InitializeQuestStep(info.id, currentQuestStepIndex, stepState.state);
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

 
