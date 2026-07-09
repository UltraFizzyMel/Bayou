using UnityEngine;
using System;

public class GameEventManager : MonoBehaviour
{

    public static GameEventManager Instance { get; private set; }

    public QuestEvents questEvents;

    public void Awake()
    {
        if (Instance != null)
        {
            Debug.Log("Found more than one Game Events Manager in the scene.");
        }
        Instance = this;

        //initialize all events
        questEvents = new QuestEvents();
    }
}
