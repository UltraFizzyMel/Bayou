using Bayou.Player;
using UnityEngine;
using System.Reflection;

public class InteractTrigger : MonoBehaviour
{
    [Header("Visual Cue")]
    // [SerializeField] private GameObject visualCue;
    [SerializeField] private string keyName;
    [SerializeField] private bool playerInRange;
    [SerializeField] private Animator animator;
    [SerializeField] KeyGateManager keyGateManager;

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
               bool key = bool.Parse(keyName);
                FieldInfo field = keyGateManager.GetType().GetField(keyName);
                if (field.GetValue(keyName).Equals(true))
                animator.SetBool("isOpen", true);

            }
        }
        //else { visualCue.SetActive(false); }
    }

    public void ChangeBoolByName(string name, bool newValue)
    {
        FieldInfo field = keyGateManager.GetType().GetField(name);

        if (field != null && field.FieldType == typeof(bool))
        {
            field.SetValue(keyGateManager, newValue);
            //Debug.Log($)
        }
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
