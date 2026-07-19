using System.Collections.Generic;
using UnityEngine;

public class QuestManager : MonoBehaviour
{
    [Header("Config")]
    [SerializeField] private bool loadQuestState = true;

    private Dictionary<string, Quest>  questMap;

    //quest start requirements
    public int netLevel = 0;
    public int rodLevel = 0;
    public int currentMainQuestLevel = 0;

    private void Awake()
    {
        questMap = CreateQuestMap();

        Quest quest = GetQuestByID("CollectFishExampleQuest");
       
    }

    private void OnEnable()
    {
        GameEventManager.Instance.questEvents.onStartQuest += StartQuest;
        GameEventManager.Instance.questEvents.onAdvanceQuest += AdvanceQuest;
        GameEventManager.Instance.questEvents.onFinishQuest += FinishQuest;

        GameEventManager.Instance.questEvents.onQuestStepStateChange += QuestStepStateChange;
    }

    private void OnDisable()
    {
        GameEventManager.Instance.questEvents.onStartQuest -= StartQuest;
        GameEventManager.Instance.questEvents.onAdvanceQuest -= AdvanceQuest;
        GameEventManager.Instance.questEvents.onFinishQuest -= FinishQuest;

        GameEventManager.Instance.questEvents.onQuestStepStateChange -= QuestStepStateChange;
    }

    private void Start()
    {
        foreach (Quest quest in questMap.Values)
        {
            //initialize any loaded quest steps.
            if(quest.state == QuestState.IN_PROGRESS)
            {
                quest.InstantiateCurrentQuestStep(this.transform);
            }

            //braodcast initial state of all quests on startup
            GameEventManager.Instance.questEvents.QuestStateChange(quest);
        }
    }

    private void ChangeQuestState(string id, QuestState state)
    {
        Quest quest = GetQuestByID(id);
        quest.state = state;
        GameEventManager.Instance.questEvents.QuestStateChange(quest);
    }

    private bool CheckRequirementsMet(Quest quest)
    {
        //start true and prove to be false
        bool meetsRequirements = true;

        //check Level Requirements
        if (netLevel < quest.info.netLevelRequirement || rodLevel < quest.info.rodLevelRequirement || currentMainQuestLevel < quest.info.mainQuestLevel )
        {
            meetsRequirements = false;
        }

        //check quest prerequisites for completion
        foreach (QuestInfoSO prerequisiteQuestInfo in quest.info.questPrerequistes) 
        { 
            if(GetQuestByID(prerequisiteQuestInfo.id).state != QuestState.FINISHED)
            {
                meetsRequirements = false;
            }
        }
        return meetsRequirements;

    }

    private void Update()
    {
        //loop through All quests to see if the quests can start.
        foreach(Quest quest in questMap.Values)
        { if (quest.state == QuestState.REQUIREMENTS_NOT_MET && CheckRequirementsMet(quest))
            { ChangeQuestState(quest.info.id, QuestState.CAN_START); }
        }
    }

    private void StartQuest(string id)
    {
        Quest quest = GetQuestByID(id);
        quest.InstantiateCurrentQuestStep(this.transform);
        ChangeQuestState(quest.info.id, QuestState.IN_PROGRESS);
    }

    private void AdvanceQuest(string id)
    {
        Quest quest = GetQuestByID(id);

        //move to next step
        quest.MoveToNextStep();

        //if there are more steps then instantiate the next step.
        if(quest.CurrentStepExists())
        {
            quest.InstantiateCurrentQuestStep(this.transform);
        }
        //if there are no more quest steps
        else
        {
            ChangeQuestState(quest.info.id, QuestState.CAN_FINISH);
        }
    }

    private void FinishQuest(string id)
    {
        Quest quest = GetQuestByID(id);
        ClaimRewards(quest);
        ChangeQuestState(quest.info.id, QuestState.FINISHED);
    }

    private void ClaimRewards(Quest quest)
    {
        //TODO!!!!!!!!!!!!!!!
        //gain money
        //gain upgrade?

    }

    private void QuestStepStateChange(string id, int stepIndex, QuestStepState questStepState)
    {
        Quest quest = GetQuestByID(id);
        quest.StoreQuestStateData(questStepState, stepIndex);
        ChangeQuestState(id, quest.state);
    }

    private Dictionary<string,Quest> CreateQuestMap()
    { 
        //Loads all QuestInfoSO Scriptable Objects under the Assets/Resources/Quests Folder
        QuestInfoSO[] allQuests = Resources.LoadAll<QuestInfoSO>("Quests"); 

        //create the quest Map
        Dictionary<string, Quest> idToQuestMap = new Dictionary<string, Quest>();
        foreach(QuestInfoSO questInfo in allQuests)
        {
            if(idToQuestMap.ContainsKey(questInfo.id))
            {
                Debug.LogWarning("Duplicate ID found when creating  quest map: " + questInfo.id);
            }
            idToQuestMap.Add(questInfo.id, LoadQuest(questInfo));
        }
        return idToQuestMap;
    }

    private Quest GetQuestByID(string id)
    {
        Quest quest = questMap[id];
        if(quest == null)
        {
            Debug.LogError("ID not found in the quest Map:" + id);
        }
        return quest;
    }

    private void OnApplicationQuit()
    {
        foreach (Quest quest in questMap.Values)
        {
           SaveQuest(quest);
        }
    }

    private void SaveQuest(Quest quest)
    {
        try
        {
            QuestData QuestData = quest.GetQuestData();
            //temporary two lines depending on save system specifics
            string serializedData = JsonUtility.ToJson(QuestData);
            PlayerPrefs.SetString(quest.info.id, serializedData);
        }
        catch(System.Exception e) 
        {
            Debug.LogError("Failed to save quest with id: " + quest.info.id + ": " + e);
        }
    }

    private  Quest LoadQuest(QuestInfoSO questInfo)
    {
        Quest quest = null;
        try
        {
            //Load quest from saved data
            if(PlayerPrefs.HasKey(questInfo.id) && loadQuestState)
            {
                string serializedData = PlayerPrefs.GetString(questInfo.id);
                QuestData questData = JsonUtility.FromJson<QuestData>(serializedData);
                quest = new Quest(questInfo, questData.state, questData.questStepIndex, questData.questStepStates);
            }
            //otherwise, initialize new quest
            else
            {
                quest = new Quest(questInfo);
            }
        }
        catch (System.Exception e) 
        {
            Debug.LogError("Failed to save quest with id: " + quest.info.id + ": " + e);
        }
        return quest;
        
    }

}
