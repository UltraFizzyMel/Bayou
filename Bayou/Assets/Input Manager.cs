using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.InputSystem;


//This script acts as a single point for all other scripts to get 
//the current input from. It uses unity's new input system and
//functions should be mapped to their corresponding controls
// using a PlayerInput Component with Unity Events.

[RequireComponent(typeof(PlayerInput))]
public class InputManager : MonoBehaviour
{
    private Vector3 moveDirection = Vector3.zero;
    private bool interactPressed = false;

    private static InputManager instance;

    private void Awake()
    {
        if (instance != null)
        {
            Debug.LogError("Found more than 1 Instance");
        }
        instance = this;
    }

   public static InputManager GetInstance()
    {
        return instance;
    }

    public void InteractPressed(InputAction.CallbackContext context)
    {
        if(context.performed)
        {
            interactPressed = true;
        }
        else if(context.canceled)
        {
            interactPressed = false;
        }
    }

    public bool GetInteractPressed()
    {
        bool result = interactPressed;
        interactPressed = false;
        return result;
    }

    public void RegisterInteractPressed()
    {
        interactPressed = false;
    }
}
