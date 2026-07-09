using Bayou.Player;
using UnityEngine;


public class QuestPoint : MonoBehaviour
{
    [Header("Quest")]
    [SerializeField] private QuestInfoSO questInfoForPoint;


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


    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<BayouCharacterMotor>() != null)
        {
            playerIsNear = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.GetComponent<BayouCharacterMotor>() != null)
        {
            playerIsNear = false;
        }
    }
}
