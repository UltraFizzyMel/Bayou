using Bayou.Player;
using Bayou.UI;
using UnityEngine;

public class DialogueTrigger : MonoBehaviour, IInteractionPromptSource
{
    [Header("Visual Cue")]
    [SerializeField] private GameObject buttonCue;
    [SerializeField] private GameObject NPCIndicator;

    [Header("Ink JSON")]
    [SerializeField] private TextAsset inkJSON;
    [SerializeField] private string knotName = "";
    [SerializeField] private string talkPrompt = "Talk";

    [SerializeField] private bool playerInRange;

    private void Awake()
    {
        playerInRange = false;
        if (buttonCue != null)
            buttonCue.SetActive(false);
        if (NPCIndicator != null)
            NPCIndicator.SetActive(true);
    }

    private void OnEnable() => InteractionPromptBroker.Register(this);
    private void OnDisable() => InteractionPromptBroker.Unregister(this);

    private void Update()
    {
        var dialogue = DialogueManager.GetInstance();
        var canTalk = playerInRange && dialogue != null && !dialogue.dialogueIsPlaying && !ShopIsOpen() &&
                      !Bayou.GameplayPause.BlocksWorldInteract;

        if (buttonCue != null)
            buttonCue.SetActive(canTalk);
        if (NPCIndicator != null)
            NPCIndicator.SetActive(!canTalk);

        if (!canTalk) return;

        var input = InputManager.GetInstance();
        if (input != null && input.GetInteractPressed())
            dialogue.EnterDialogueMode(inkJSON, knotName);
    }

    public bool TryGetInteractionPrompt(out InteractionPrompt prompt)
    {
        prompt = default;
        var dialogue = DialogueManager.GetInstance();
        if (!playerInRange || dialogue == null || dialogue.dialogueIsPlaying || ShopIsOpen() ||
            Bayou.GameplayPause.BlocksWorldInteract)
            return false;

        prompt = new InteractionPrompt("E", talkPrompt, 60, DistToPlayerSq());
        return true;
    }

    private static bool ShopIsOpen()
    {
        var shop = Bayou.Inventory.Shop.ShopUIController.ActiveShop;
        return shop != null && shop.IsOpen;
    }

    private float DistToPlayerSq()
    {
        var p = PlayerLocator.Transform;
        if (p == null) return 0f;
        var d = transform.position - p.position;
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
