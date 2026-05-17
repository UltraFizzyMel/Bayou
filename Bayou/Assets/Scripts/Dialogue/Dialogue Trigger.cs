using Bayou.Player;
using UnityEngine;

public class DialogueTrigger : MonoBehaviour
{
    [Header("Visual Cue")]
    [SerializeField] private GameObject visualCue;

    [Header("Ink JSON")]
    [SerializeField] private TextAsset inkJSON;
    [SerializeField] private string knotName = "";

    [SerializeField] private bool playerInRange;

    private void Awake()
    {
        playerInRange = false;
        visualCue.SetActive(false);
    }

    private void Update()
    {
        if (playerInRange && !DialogueManager.GetInstance().dialogueIsPlaying)
        {
            visualCue.SetActive(true);
            //Debug.Log(InputManager.GetInstance().GetInteractPressed());
            if (InputManager.GetInstance().GetInteractPressed())
            {
                DialogueManager.GetInstance().EnterDialogueMode(inkJSON, knotName);
            }
        }
        else { visualCue.SetActive(false); }
    }

    private void OnTriggerEnter(Collider other)
    {
        if(other.TryGetComponent<BayouCharacterMotor>(out BayouCharacterMotor bayouCharacterMotor))
        {
            playerInRange = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.TryGetComponent<BayouCharacterMotor>(out BayouCharacterMotor bayouCharacterMotor))
        {
            playerInRange = false;
        }
    }
}
