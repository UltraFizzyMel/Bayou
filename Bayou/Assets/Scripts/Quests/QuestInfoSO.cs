using UnityEngine;

[CreateAssetMenu(fileName = "QuestInfoSO", menuName = "ScriptableObjects/QuestInfoSO", order = 1)]
public class QuestInfoSO : ScriptableObject
{
    [field: SerializeField] public string id { get; private set; }

    [Header("General")]
    public string displayName;

    [Header("Requirements")]
    public int netLevelRequirement;
    public int rodLevelRequirement;
    public int mainQuestLevel;

    public QuestInfoSO[] questPrerequistes;

    [Header("Steps")]
    public GameObject[] questStepPrefabs;

    [Header("Start")]
    [Tooltip("Begin this quest automatically when the scene loads (if requirements are met).")]
    public bool autoStart;
    [Tooltip("When the last step finishes, mark the quest finished (no turn-in).")]
    public bool autoComplete;

    [Header("Rewards")]
    public int moneyReward;
    public string MiscReward;


    //ensure the id is always the name of the scriptable object assset
    private void OnValidate()
    {
#if UNITY_EDITOR
        id = this.name;
        UnityEditor.EditorUtility.SetDirty(this);
        #endif
    }
}

