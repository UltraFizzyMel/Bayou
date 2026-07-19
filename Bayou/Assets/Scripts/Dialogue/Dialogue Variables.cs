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

    public void StartListening(Story story )
    { 
        // Important that VariablesToStory is before assigning the listener!!!
        VariablesToStory(story);
        story.variablesState.variableChangedEvent += VariableChanged;
    }

    public void StopListening(Story story)
    {
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
    }

    private  void VariablesToStory(Story story)
    {
        foreach(KeyValuePair<string, Ink.Runtime.Object> variable in variables)
        {
            story.variablesState.SetGlobal(variable.Key, variable.Value);
        }
    }
}
