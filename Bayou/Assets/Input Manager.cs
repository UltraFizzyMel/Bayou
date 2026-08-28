using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
[DefaultExecutionOrder(-200)]
public class InputManager : MonoBehaviour
{
    private bool interactPressed;
    private bool dialogueAdvancePressed;

    private PlayerInput playerInput;
    private InputAction castAction;

    private static InputManager instance;

    private void Awake()
    {
        if (instance != null)
            Debug.LogError("Found more than 1 Instance");
        instance = this;
        playerInput = GetComponent<PlayerInput>();
    }

    private void OnEnable()
    {
        BindCast();
    }

    private void OnDisable()
    {
        UnbindCast();
    }

    private void OnDestroy()
    {
        UnbindCast();
        if (instance == this)
            instance = null;
    }

    public static InputManager GetInstance()
    {
        return instance;
    }

    private void BindCast()
    {
        UnbindCast();
        if (playerInput == null)
            playerInput = GetComponent<PlayerInput>();

        castAction = playerInput != null ? playerInput.actions?.FindAction("Cast") : null;
        if (castAction != null)
            castAction.performed += OnCastPerformed;
    }

    private void UnbindCast()
    {
        if (castAction != null)
            castAction.performed -= OnCastPerformed;
        castAction = null;
    }

    private void OnCastPerformed(InputAction.CallbackContext context)
    {
        if (!context.performed)
            return;
        if (IsDialoguePlaying())
            dialogueAdvancePressed = true;
    }

    /// <summary>
    /// PlayerInput Unity Event for Interact (E, and now Space/Enter).
    /// Never clear on canceled — that was wiping latched advance presses.
    /// </summary>
    public void InteractPressed(InputAction.CallbackContext context)
    {
        if (!context.performed)
            return;

        interactPressed = true;
        if (IsDialoguePlaying())
            dialogueAdvancePressed = true;
    }

    /// <summary>
    /// World interact (talk, gates, pickups). Returns false while dialogue owns the press.
    /// </summary>
    public bool GetInteractPressed()
    {
        if (IsDialoguePlaying())
            return false;

        bool result = interactPressed;
        interactPressed = false;
        return result;
    }

    /// <summary>Dialogue-only: E, Space, Enter, or left click (Cast) this press.</summary>
    public bool ConsumeDialogueAdvance()
    {
        bool result = dialogueAdvancePressed || interactPressed;
        dialogueAdvancePressed = false;
        interactPressed = false;
        return result;
    }

    public void RegisterInteractPressed()
    {
        interactPressed = false;
        dialogueAdvancePressed = false;
    }

    private static bool IsDialoguePlaying()
    {
        var dialogue = DialogueManager.GetInstance();
        return dialogue != null && dialogue.dialogueIsPlaying;
    }
}
