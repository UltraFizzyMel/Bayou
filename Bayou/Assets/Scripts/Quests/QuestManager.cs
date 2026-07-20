using System.Collections.Generic;
using UnityEngine;

public class QuestManager : MonoBehaviour
{
    [Header("Config")]
    [SerializeField] private bool loadQuestState = true;

    private Dictionary<string, Quest> questMap;
    private bool _subscribed;

    //quest start requirements
    public int netLevel = 0;
    public int rodLevel = 0;
    public int currentMainQuestLevel = 0;

    public static QuestManager Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
        questMap = CreateQuestMap();
    }

    private void OnEnable() => TrySubscribe();

    private void OnDisable()
    {
        Unsubscribe();
        _subscribed = false;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Start()
    {
        TrySubscribe();

        foreach (Quest quest in questMap.Values)
        {
            if (quest == null) continue;

            if (quest.state == QuestState.IN_PROGRESS)
                quest.InstantiateCurrentQuestStep(this.transform);

            GameEventManager.Instance?.questEvents?.QuestStateChange(quest);
        }
    }

    private void Update()
    {
        if (!_subscribed)
            TrySubscribe();

        foreach (Quest quest in questMap.Values)
        {
            if (quest != null &&
                quest.state == QuestState.REQUIREMENTS_NOT_MET &&
                CheckRequirementsMet(quest))
            {
                ChangeQuestState(quest.info.id, QuestState.CAN_START);
            }
        }
    }

    private void TrySubscribe()
    {
        var events = GameEventManager.Instance != null ? GameEventManager.Instance.questEvents : null;
        if (events == null || _subscribed) return;

        events.onStartQuest += HandleStartQuestEvent;
        events.onAdvanceQuest += AdvanceQuest;
        events.onFinishQuest += FinishQuest;
        events.onQuestStepStateChange += QuestStepStateChange;
        _subscribed = true;
    }

    private void Unsubscribe()
    {
        var events = GameEventManager.Instance != null ? GameEventManager.Instance.questEvents : null;
        if (events == null) return;

        events.onStartQuest -= HandleStartQuestEvent;
        events.onAdvanceQuest -= AdvanceQuest;
        events.onFinishQuest -= FinishQuest;
        events.onQuestStepStateChange -= QuestStepStateChange;
    }

    private void HandleStartQuestEvent(string id) => StartQuest(id);

    private void ChangeQuestState(string id, QuestState state)
    {
        if (!TryGetQuest(id, out var quest))
        {
            Debug.LogError($"[QuestManager] Unknown quest id '{id}'.");
            return;
        }

        quest.state = state;
        GameEventManager.Instance?.questEvents?.QuestStateChange(quest);
        Debug.Log($"[QuestManager] {id} → {state}");
    }

    private bool CheckRequirementsMet(Quest quest)
    {
        bool meetsRequirements = true;

        if (netLevel < quest.info.netLevelRequirement ||
            rodLevel < quest.info.rodLevelRequirement ||
            currentMainQuestLevel < quest.info.mainQuestLevel)
        {
            meetsRequirements = false;
        }

        if (quest.info.questPrerequistes != null)
        {
            foreach (QuestInfoSO prerequisiteQuestInfo in quest.info.questPrerequistes)
            {
                if (prerequisiteQuestInfo == null) continue;
                if (!TryGetQuest(prerequisiteQuestInfo.id, out var prereq) ||
                    prereq.state != QuestState.FINISHED)
                {
                    meetsRequirements = false;
                }
            }
        }

        return meetsRequirements;
    }

    /// <summary>Called from Ink / gameplay. Safe to call even if the event bus missed the subscribe.</summary>
    public void StartQuest(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            Debug.LogError("[QuestManager] StartQuest failed — empty id.");
            return;
        }

        id = id.Trim();
        if (!TryGetQuest(id, out var quest))
        {
            Debug.LogError($"[QuestManager] StartQuest failed — unknown id '{id}'.");
            return;
        }

        if (quest.state == QuestState.IN_PROGRESS || quest.state == QuestState.CAN_FINISH)
        {
            // Already active — still notify HUD.
            GameEventManager.Instance?.questEvents?.QuestStateChange(quest);
            return;
        }

        if (quest.state == QuestState.FINISHED)
        {
            Debug.Log($"[QuestManager] StartQuest ignored — '{id}' already finished.");
            return;
        }

        // Allow start from CAN_START or REQUIREMENTS_NOT_MET (Ink can ask before Update promotes state).
        ChangeQuestState(quest.info.id, QuestState.IN_PROGRESS);
        quest.InstantiateCurrentQuestStep(this.transform);
    }

    private void AdvanceQuest(string id)
    {
        if (!TryGetQuest(id, out var quest)) return;

        quest.MoveToNextStep();

        if (quest.CurrentStepExists())
            quest.InstantiateCurrentQuestStep(this.transform);
        else
            ChangeQuestState(quest.info.id, QuestState.CAN_FINISH);
    }

    private void FinishQuest(string id)
    {
        if (!TryGetQuest(id, out var quest)) return;
        ClaimRewards(quest);
        ChangeQuestState(quest.info.id, QuestState.FINISHED);
    }

    private void ClaimRewards(Quest quest)
    {
        // Rewards currently granted from Ink (money) / shop unlock via dialogue.
    }

    private void QuestStepStateChange(string id, int stepIndex, QuestStepState questStepState)
    {
        if (!TryGetQuest(id, out var quest)) return;
        quest.StoreQuestStateData(questStepState, stepIndex);
        // Notify HUD of progress without re-firing onQuestStepStateChange (would recurse).
        GameEventManager.Instance?.questEvents?.QuestStateChange(quest);
    }

    private Dictionary<string, Quest> CreateQuestMap()
    {
        QuestInfoSO[] allQuests = Resources.LoadAll<QuestInfoSO>("Quests");
        var idToQuestMap = new Dictionary<string, Quest>();

        foreach (QuestInfoSO questInfo in allQuests)
        {
            if (questInfo == null || string.IsNullOrWhiteSpace(questInfo.id)) continue;

            if (idToQuestMap.ContainsKey(questInfo.id))
            {
                Debug.LogWarning("Duplicate ID found when creating quest map: " + questInfo.id);
                continue;
            }

            var loaded = LoadQuest(questInfo);
            if (loaded != null)
                idToQuestMap.Add(questInfo.id, loaded);
        }

        Debug.Log($"[QuestManager] Loaded {idToQuestMap.Count} quests.");
        return idToQuestMap;
    }

    private Quest GetQuestByID(string id)
    {
        TryGetQuest(id, out var quest);
        return quest;
    }

    public bool TryGetPrimaryActiveQuest(out Quest quest)
    {
        quest = null;
        if (questMap == null) return false;

        if (questMap.TryGetValue("SnapperAndMollyQuest", out var caliste) &&
            caliste != null && caliste.IsActiveForHud)
        {
            quest = caliste;
            return true;
        }

        foreach (var candidate in questMap.Values)
        {
            if (candidate != null && candidate.state == QuestState.IN_PROGRESS)
            {
                quest = candidate;
                return true;
            }
        }

        foreach (var candidate in questMap.Values)
        {
            if (candidate != null && candidate.state == QuestState.CAN_FINISH)
            {
                quest = candidate;
                return true;
            }
        }

        return false;
    }

    public bool TryGetQuest(string id, out Quest quest)
    {
        quest = null;
        if (questMap == null || string.IsNullOrWhiteSpace(id)) return false;
        return questMap.TryGetValue(id, out quest) && quest != null;
    }

    public static QuestManager Resolve() =>
        Instance != null ? Instance : Object.FindFirstObjectByType<QuestManager>();

    private void OnApplicationQuit()
    {
        if (questMap == null) return;
        foreach (Quest quest in questMap.Values)
        {
            if (quest != null)
                SaveQuest(quest);
        }
    }

    private void SaveQuest(Quest quest)
    {
        try
        {
            QuestData questData = quest.GetQuestData();
            string serializedData = JsonUtility.ToJson(questData);
            PlayerPrefs.SetString(quest.info.id, serializedData);
        }
        catch (System.Exception e)
        {
            Debug.LogError("Failed to save quest with id: " + quest.info.id + ": " + e);
        }
    }

    private Quest LoadQuest(QuestInfoSO questInfo)
    {
        try
        {
            if (PlayerPrefs.HasKey(questInfo.id) && loadQuestState)
            {
                string serializedData = PlayerPrefs.GetString(questInfo.id);
                QuestData questData = JsonUtility.FromJson<QuestData>(serializedData);
                if (questData != null)
                    return new Quest(questInfo, questData.state, questData.questStepIndex, questData.questStepStates);
            }

            return new Quest(questInfo);
        }
        catch (System.Exception e)
        {
            Debug.LogError("Failed to load quest with id: " + questInfo.id + ": " + e);
            return new Quest(questInfo);
        }
    }
}
