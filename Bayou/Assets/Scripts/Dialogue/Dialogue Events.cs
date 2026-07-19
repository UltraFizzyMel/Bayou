using System;
using UnityEngine;

public class DialogueEvents : MonoBehaviour
{
    public event Action<string, Ink.Runtime.Object> onUpdateDialogueVariable;

    public void UpdateDialogueVariable(string name, Ink.Runtime.Object value)
    {
        if (onUpdateDialogueVariable != null)
        {
            onUpdateDialogueVariable(name, value);
        }
    }
}
