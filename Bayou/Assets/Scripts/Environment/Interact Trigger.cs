using Bayou.Player;
using UnityEngine;

public class InteractTrigger : MonoBehaviour
{
    [Header("Visual Cue")]
   // [SerializeField] private GameObject visualCue;

    [SerializeField] private bool playerInRange;

    private void Awake()
    {
        playerInRange = false;
        //visualCue.SetActive(false);
    }

    private void Update()
    {
        if (playerInRange)
        {
            //visualCue.SetActive(true);
            //Debug.Log(InputManager.GetInstance().GetInteractPressed());
            if (InputManager.GetInstance().GetInteractPressed())
            {
                
               
            }
        }
        //else { visualCue.SetActive(false); }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent<BayouCharacterMotor>(out BayouCharacterMotor bayouCharacterMotor))
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
