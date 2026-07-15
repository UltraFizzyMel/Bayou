using Bayou.Player;
using UnityEngine;


public class QuestPoint : MonoBehaviour
{
    [Header("Quest")]
    [SerializeField] private QuestInfoSO questInfoForPoint;

    [Header("Config")]
    [SerializeField] private bool startPoint = true;
    [SerializeField] private bool finishPoint = true;


    private bool playerIsNear = false;
    private string questId;
    private QuestState currentQuestState;

    private  void Awake()
    {
         questId = questInfoForPoint.id;
    }

    private void OnEnable()
    {
        GameEventManager.Instance.questEvents.onQuestStateChange += QuestStateChange;
        //GameEventManager.Instance.
    }

    private void OnDisable()
    {
        GameEventManager.Instance.questEvents.onQuestStateChange -= QuestStateChange;
    }

    private void QuestStateChange(Quest quest)
    {
        //only update the quest state if this point has the coresponding quest
        if (quest.info.id.Equals(questId))
        {
            currentQuestState = quest.state;
            Debug.Log("Quest with id: " + questId + "updated to state: " + currentQuestState);
        }
    }

    private void CheckQuest()
    {
        if (currentQuestState.Equals(QuestState.CAN_START) && startPoint)
        {
            GameEventManager.Instance.questEvents.StartQuest(questId);
        }
        else if(currentQuestState.Equals(QuestState.CAN_FINISH) && finishPoint)
        {
            GameEventManager.Instance.questEvents.FinishQuest(questId);
        }
    }
   

    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<BayouCharacterMotor>() != null)
        {
            playerIsNear = true;
        }
        CheckQuest();
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.GetComponent<BayouCharacterMotor>() != null)
        {
            playerIsNear = false;
        }
    }
}
