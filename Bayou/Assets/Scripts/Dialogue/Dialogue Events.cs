using System;

/// <summary>
/// Plain C# event bus for dialogue variables (not a MonoBehaviour).
/// </summary>
public class DialogueEvents
{
    public event Action<string, Ink.Runtime.Object> onUpdateDialogueVariable;

    public void UpdateDialogueVariable(string name, Ink.Runtime.Object value) =>
        onUpdateDialogueVariable?.Invoke(name, value);
}
