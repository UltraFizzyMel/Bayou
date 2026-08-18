using Bayou.Player;
using Bayou.UI;
using UnityEngine;

public class DialogueTrigger : MonoBehaviour, IInteractionPromptSource
{
    [Header("Visual Cue")]
    [SerializeField] private GameObject visualCue;

    [Header("Ink JSON")]
    [SerializeField] private TextAsset inkJSON;
    [SerializeField] private string knotName = "";
    [SerializeField] private string talkPrompt = "Talk";

    [SerializeField] private bool playerInRange;

    private void Awake()
    {
        playerInRange = false;
        if (visualCue != null)
            visualCue.SetActive(false);
    }

    private void OnEnable() => InteractionPromptBroker.Register(this);
    private void OnDisable() => InteractionPromptBroker.Unregister(this);

    private void Update()
    {
        var dialogue = DialogueManager.GetInstance();
        var canTalk = playerInRange && dialogue != null && !dialogue.dialogueIsPlaying;

        if (visualCue != null)
            visualCue.SetActive(canTalk);

        if (!canTalk) return;

        var input = InputManager.GetInstance();
        if (input != null && input.GetInteractPressed())
            dialogue.EnterDialogueMode(inkJSON, knotName);
    }

    public bool TryGetInteractionPrompt(out InteractionPrompt prompt)
    {
        prompt = default;
        var dialogue = DialogueManager.GetInstance();
        if (!playerInRange || dialogue == null || dialogue.dialogueIsPlaying)
            return false;

        prompt = new InteractionPrompt("E", talkPrompt, 60, DistToPlayerSq());
        return true;
    }

    private float DistToPlayerSq()
    {
        var p = GameObject.FindGameObjectWithTag("Player");
        if (p == null) return 0f;
        var d = transform.position - p.transform.position;
        d.y = 0f;
        return d.sqrMagnitude;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent<BayouCharacterMotor>(out _) ||
            other.GetComponentInParent<BayouCharacterMotor>() != null)
            playerInRange = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.TryGetComponent<BayouCharacterMotor>(out _) ||
            other.GetComponentInParent<BayouCharacterMotor>() != null)
            playerInRange = false;
    }
}
