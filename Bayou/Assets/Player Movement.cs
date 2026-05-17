using System.Globalization;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{ 
 [Header("PlayerComponents")]
[SerializeField] private Transform cameraPivot;     //empty child at head height
[SerializeField] private Camera playerCamera;   //child camera
[SerializeField] private Animator animator;

[Header("Player Settings")]
[SerializeField] private float moveSpeed = 5f;
[SerializeField] private float lookSensitivity = 2f;
[SerializeField] private float maxPitch = 80f;

[Header("Animator Params")]
[SerializeField] private string speedParam = "Speed";

private PlayerInput pi;
private InputAction moveAction;
private InputAction lookAction;
private InputAction jumpAction;
private CharacterController cc;

private float pitch;    //Current up/down camera rotation


public void Start()
{
    cc = GetComponent<CharacterController>();
    pi = GetComponent<PlayerInput>();

    moveAction = pi.actions["Move"];
    lookAction = pi.actions["Look"];
    jumpAction = pi.actions["Jump"];
    moveAction.Enable();
    lookAction.Enable();
    jumpAction.Enable();

    if (playerCamera) playerCamera.enabled = true;
}

private void Update()
{
    //Move (X/Z)
    if(DialogueManager.GetInstance().dialogueIsPlaying)
        {
            return;
        }

    Vector2 m = moveAction.ReadValue<Vector2>();
    if (moveAction != null)
    {

        Vector3 move = transform.right * m.x + transform.forward * m.y;
        cc.Move(move * moveSpeed * Time.deltaTime);
    }

    //Look
    if (lookAction != null)
    {
        Vector2 look = lookAction.ReadValue<Vector2>() * lookSensitivity;
        transform.Rotate(0f, look.x, 0f); // yaw = rotate the whole character left/right around y axis


        pitch -= look.y;// pitch = rotate the pamera pivot up/down(invert look.y by subtracting)
        pitch = Mathf.Clamp(pitch, -maxPitch, maxPitch);//clamp camera so player doesnt turn over
        cameraPivot.localEulerAngles = new Vector3(pitch, 0f, 0f); // Apply pitch to the camera pivot only(keeps body upright)

        // drive animation from actual movement input
        if (animator) animator.SetFloat(speedParam, m.magnitude); // 0 = idle; >0 = Walking m.mag turns 2D input into a single number = "how much is player trying to move"
    }
}
}