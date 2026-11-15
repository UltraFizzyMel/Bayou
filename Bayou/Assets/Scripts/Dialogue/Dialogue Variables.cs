using UnityEngine;
using Ink.Runtime;
using System.Collections.Generic;

public class DialogueVariables 
{
    public Dictionary<string, Ink.Runtime.Object> variables { get; private set; }

    private Story globalVariablesStory;

    private const string saveVariablesKey = "INK_VARIABLES";

    public DialogueVariables(TextAsset loadGlobalsJSON)
    {
        //create the story
        globalVariablesStory = new Story(loadGlobalsJSON.text);

        /*
         * If(PlayerPrefs.HasKey(saveVariablesKey);
         * {
         * string JsonState = Playerprefs.GetString(saveVariableKey);
         * globalVariablesStory.state.LoadJson(jsonState);
         * }
          */

        //initialize the dictionary
        variables = new Dictionary<string, Ink.Runtime.Object>();
        foreach (string name in globalVariablesStory.variablesState)
        {
            Ink.Runtime.Object value = globalVariablesStory.variablesState.GetVariableWithName(name);
            variables.Add(name, value);
            Debug.Log("Initialized global dialogue variable: " + name + " = " + value);

        }
    }

    public void SaveVariables()
    {
        if (globalVariablesStory != null)
        {
            //Load the current state of all of our variables to the globals story
            VariablesToStory(globalVariablesStory);
            //NOTE: Replace this with an actual save/load system
            //PlayerPrefs.SetString(saveVariablesKey, globalVariablesStory.state.ToJson());
        }
    }

    private Story _listening;
    private bool _mirroring;

    public void StartListening(Story story )
    { 
        // Important that VariablesToStory is before assigning the listener!!!
        _listening = story;
        VariablesToStory(story);
        story.variablesState.variableChangedEvent += VariableChanged;
    }

    public void StopListening(Story story)
    {
        if (_listening == story)
            _listening = null;
        story.variablesState.variableChangedEvent -= VariableChanged;
    }

    public void VariableChanged(string name, Ink.Runtime.Object value)
    {
        //only maintain variables that were initialized from the globals ink file;
        if(variables.ContainsKey(name)) 
        {
            variables.Remove(name);
            variables.Add(name, value );
            Debug.Log("Updated dialogue variable: "+ name + "  " + value);
        }

        MirrorAlias(name, value);
    }

    private static string AliasOf(string name)
    {
        switch (name)
        {
            case "accordian_returned": return "accordion_returned";
            case "accordion_returned": return "accordian_returned";
            case "accordian_disposed": return "accordion_disposed";
            case "accordion_disposed": return "accordian_disposed";
            case "is_olivier_alive": return "is_Olivier_alive";
            case "is_Olivier_alive": return "is_olivier_alive";
            default: return null;
        }
    }

    private void MirrorAlias(string name, Ink.Runtime.Object value)
    {
        if (_mirroring) return;
        var other = AliasOf(name);
        if (string.IsNullOrEmpty(other) || !variables.ContainsKey(other))
            return;

        _mirroring = true;
        variables.Remove(other);
        variables.Add(other, value);
        if (_listening != null && _listening.variablesState.GlobalVariableExistsWithName(other))
            _listening.variablesState.SetGlobal(other, value);
        _mirroring = false;
    }

    private  void VariablesToStory(Story story)
    {
        foreach(KeyValuePair<string, Ink.Runtime.Object> variable in variables)
        {
            story.variablesState.SetGlobal(variable.Key, variable.Value);
        }
    }
}
