using Ink.Runtime;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class DialogueManager : MonoBehaviour
{
    [Header("Params")]
    [SerializeField] private float typingSpeed = 0.04f;

    [Header("Load Globals JSON")]

    [SerializeField] private TextAsset loadGlobalsJSON;

    [Header("Dialogue UI")]
    [SerializeField] private GameObject dialoguePanel;
    [SerializeField] private GameObject continueIcon;
    [SerializeField] private TextMeshProUGUI dialogueText;
    [SerializeField] private TextMeshProUGUI displayNameText;
    [SerializeField] private Animator portraitAnimator;
    [SerializeField] private Animator backgroundAnimator;

    private Animator layoutAnimator;

    [Header("Choices UI")]
    [SerializeField] private GameObject[] choices;

    private TextMeshProUGUI[] choicesText;

    //story object that allows us to traverse ink dialogue file
    private Story currentStory;

    public bool dialogueIsPlaying { get; private set; }

    private InkExternalFunctions inkExternalFunctions;


    private bool canContinueToNextLine = false;

    private Coroutine displayLineCoroutine;

    private static DialogueManager instance;

    private const string SPEAKER_TAG = "speaker";
    private const string PORTRAIT_TAG = "portrait";
    private const string LAYOUT_TAG = "layout";
    private const string BACKGROUND_TAG = "background";

    private DialogueVariables dialogueVariables;
    private bool _eventsSubscribed;

    private void Awake()
    {
        if (instance != null)
        {
            Debug.LogWarning("Found more than one Dialogue Manager in the scene");
        }
        instance = this;

        dialogueVariables = new DialogueVariables(loadGlobalsJSON);
        
    }

    public static DialogueManager GetInstance()
    {
        return instance;
    }

    public void Start()
    {
        dialogueIsPlaying = false;
        dialoguePanel.SetActive(false);

        //get the layout animator
        layoutAnimator = dialoguePanel.GetComponent<Animator>();

        //get all of the choices text
        choicesText = new TextMeshProUGUI[choices.Length];
        int index = 0;
        foreach (GameObject choice in choices)
        {
            choicesText[index] = choice.GetComponentInChildren<TextMeshProUGUI>();
            index++;
        }
    }

    private void OnEnable() => TrySubscribeEvents();

    private void OnDisable()
    {
        UnsubscribeEvents();
        _eventsSubscribed = false;
    }

    private void TrySubscribeEvents()
    {
        var events = GameEventManager.Instance;
        if (events == null || _eventsSubscribed) return;
        events.dialogueEvents.onUpdateDialogueVariable += UpdateDialogueVariable;
        events.questEvents.onQuestStateChange += QuestStateChange;
        _eventsSubscribed = true;
    }

    private void UnsubscribeEvents()
    {
        var events = GameEventManager.Instance;
        if (events == null || !_eventsSubscribed) return;
        events.dialogueEvents.onUpdateDialogueVariable -= UpdateDialogueVariable;
        events.questEvents.onQuestStateChange -= QuestStateChange;
    }

    private void QuestStateChange(Quest quest)
    {
        if (quest?.info == null) return;
        ApplyQuestStateToDialogue(quest.info.id, quest.state);
    }

    private void SyncQuestStatesIntoDialogueVariables()
    {
        var manager = QuestManager.Resolve();
        if (manager == null) return;

        // Known quest ids from globals.ink — push live QuestManager state into Ink.
        string[] ids =
        {
            "CollectPondItemQuest",
            "CollectLanternQuest",
            "SnapperAndMollyQuest"
        };

        foreach (var id in ids)
        {
            if (manager.TryGetQuest(id, out var quest) && quest != null)
                ApplyQuestStateToDialogue(id, quest.state);
        }
    }

    private void ApplyQuestStateToDialogue(string questId, QuestState state)
    {
        var value = new StringValue(state.ToString());
        dialogueVariables?.VariableChanged(questId + "State", value);
        GameEventManager.Instance?.dialogueEvents?.UpdateDialogueVariable(questId + "State", value);
    }

    private void UpdateDialogueVariable(string name, Ink.Runtime.Object value)
    {
       
        dialogueVariables.VariableChanged(name, value);
    }

    public void Update()
    {
        if (!_eventsSubscribed)
            TrySubscribeEvents();

        //Debug.Log(dialogueIsPlaying);
        //return right away if dialogue isn't playing
        if (!dialogueIsPlaying)
        {
            return;
        }

        //handle continuing to the next line in the dialogue when submit is pressed
        if (canContinueToNextLine 
            && currentStory.currentChoices.Count == 0 
            && InputManager.GetInstance().GetInteractPressed())
        {
            ContinueStory();
        }
        
    }

    public void EnterDialogueMode(TextAsset inkJSON, string knotName)
    {
        
        currentStory = new Story(inkJSON.text);

        inkExternalFunctions = new InkExternalFunctions();
        inkExternalFunctions.Bind(currentStory);

        // Keep Ink quest*State vars in sync with QuestManager (Start() order can miss this).
        SyncQuestStatesIntoDialogueVariables();

        if (knotName != "")
        {
            currentStory.ChoosePathString(knotName);
        }
        
        dialogueIsPlaying = true;
        dialoguePanel.SetActive(true);

        dialogueVariables.StartListening(currentStory);

        //reset portrait, layout and speaker
        displayNameText.text = "???";
        portraitAnimator.Play("default");
        layoutAnimator.Play("default");

        ContinueStory();
    }

    private IEnumerator ExitDialogueMode()
    {
        // Small delay so the last line can finish reading, then tear the UI down.
        yield return new WaitForSeconds(0.05f);
        ForceExitDialogueImmediate();
    }

    /// <summary>
    /// Immediately ends dialogue and hides the dialogue UI.
    /// </summary>
    public void ForceExitDialogueImmediate()
    {
        HideDialogueUi();

        if (currentStory != null)
        {
            try
            {
                inkExternalFunctions?.Unbind(currentStory);
            }
            catch (System.Exception)
            {
                // Already unbound.
            }

            dialogueVariables?.StopListening(currentStory);
            currentStory = null;
        }
    }

    /// <summary>Hides panels/choices without unbinding Ink mid-external-call.</summary>
    private void HideDialogueUi()
    {
        if (displayLineCoroutine != null)
        {
            StopCoroutine(displayLineCoroutine);
            displayLineCoroutine = null;
        }

        dialogueIsPlaying = false;
        canContinueToNextLine = false;
        HideChoices();
        if (continueIcon != null)
            continueIcon.SetActive(false);
        if (dialogueText != null)
            dialogueText.text = "";
        if (displayNameText != null)
            displayNameText.text = "";

        if (portraitAnimator != null)
            portraitAnimator.Play("default");
        if (layoutAnimator != null)
            layoutAnimator.Play("default");
        if (backgroundAnimator != null)
            backgroundAnimator.Play("default");

        if (dialoguePanel != null)
            dialoguePanel.SetActive(false);
    }

    private Coroutine _openShopRoutine;

    /// <summary>Ink <c>OpenShop()</c> — close dialogue UI now, open shop next frame.</summary>
    public void QueueOpenShop()
    {
        // Hide immediately (don't Unbind yet — we're still inside the Ink external).
        HideDialogueUi();

        if (_openShopRoutine != null)
            StopCoroutine(_openShopRoutine);
        _openShopRoutine = StartCoroutine(OpenShopAfterDialogueRoutine());
    }

    private IEnumerator OpenShopAfterDialogueRoutine()
    {
        // Let the Ink external call stack unwind, then fully tear down + open shop.
        yield return null;
        _openShopRoutine = null;
        ForceExitDialogueImmediate();
        InkExternalFunctions.OpenShopImmediate();
    }

    private void ContinueStory()
    {
        if (currentStory == null || !dialogueIsPlaying)
            return;

        if (currentStory.canContinue)
        {
            //set Text for the current dialogue line
            if (displayLineCoroutine != null)
            {
                StopCoroutine(displayLineCoroutine);
            }

            displayLineCoroutine = StartCoroutine(DisplayLine(currentStory.Continue()));

            //handle tags
            if (currentStory != null)
                HandleTags(currentStory.currentTags);
        }
        else
        {
            StartCoroutine(ExitDialogueMode());
        }
    }

    private IEnumerator DisplayLine(string line)
    {
        
    //set the text to the full line, but set the visible characters to 0
    dialogueText.text = line;

        dialogueText.maxVisibleCharacters = 0;
        //hide items while text is typing
        continueIcon.SetActive(false);
        HideChoices();

        canContinueToNextLine = false;

        bool isAddingRichTextTag = false;

        while (IsLineBlank(line) && currentStory.canContinue)
        {
            //handle tags
            HandleTags(currentStory.currentTags);
            line = currentStory.Continue();
            dialogueText.text = line;

            dialogueText.maxVisibleCharacters = 0;
            //hide items while text is typing
            continueIcon.SetActive(false);
            HideChoices();

            canContinueToNextLine = false;

            isAddingRichTextTag = false;
        }

        //display each letter one at a time
        foreach (char letter in line.ToCharArray())
        {
            //if the dialogue button is pressed, finish displaying line right away
            if(InputManager.GetInstance().GetInteractPressed())
            {
                dialogueText.maxVisibleCharacters = line.Length;
                break;
            }

            //check for rich text tag, if found, add it without waiting
            if(letter == '<' || isAddingRichTextTag) 
            {
                isAddingRichTextTag = true;
                
                if (letter == '>')
                {
                    isAddingRichTextTag = false;
                }
            }
            //if not rich text, add the next letter and wait a small time.
            else
            {
                dialogueText.maxVisibleCharacters++;
                yield return new WaitForSeconds(typingSpeed);
            }
        }

        // Dialogue may have been force-closed (e.g. OpenShop) while this line was typing.
        if (!dialogueIsPlaying || currentStory == null)
            yield break;

        //actions to take after line has finished displaying
        continueIcon.SetActive(true);
        //display choices, if any, for this dialogue
        DisplayChoices();

        canContinueToNextLine = true;
    }

    private void HandleTags(List<string> currentTags)
    {
        //loop through each tag and handle it accordingly
        foreach(string tag in currentTags) 
        {
            //parse the tag
            string[] splitTag = tag.Split(':');
            if (splitTag.Length != 2)             
            {
                Debug.LogError("Tag could not be appropriately parsed: " + tag);
                continue;
            }
            //trim method cleans up any whitespace on beginning or end of string
            string tagKey = splitTag[0].Trim();
            string tagValue = splitTag[1].Trim();

            //handle the tag
            switch (tagKey)
            {
                case SPEAKER_TAG:
                    displayNameText.text = tagValue;
                    break;
                case PORTRAIT_TAG:
                    portraitAnimator.Play(tagValue);
                    break;
                case LAYOUT_TAG:
                    layoutAnimator.Play(tagValue);
                    break;
                case BACKGROUND_TAG:
                    backgroundAnimator.Play(tagValue);
                    Debug.Log(tagValue);
                    break;
                default:
                    Debug.LogWarning("Tag came in but is not currently being handled:" + tag);
                    break;
            }
        }
    }

    private void DisplayChoices()
    {
        List<Choice> currentChoices = currentStory.currentChoices;

        //defensive check to make sure the UI can support the number of choices coming in.
        if (currentChoices.Count > choices.Length)
        {
            Debug.LogError("More choices were given then the UI can support. Number of choices given: " + currentChoices.Count);
        }

        int index = 0;
        //enable and initialize choices up to the amount of choices for this line of dialogue.
        foreach (Choice choice in currentChoices)
        {
            choices[index].gameObject.SetActive(true);
            choicesText[index].text = string.IsNullOrWhiteSpace(choice.text) ? "…" : choice.text.Trim();
            index++;
        }
        //Go through the remaining choices the UI supports and make sure they're hidden
        for (int i = index; i < choices.Length; i++)
        {
            choices[i].gameObject.SetActive(false);
        }

        if (index > 0)
            StartCoroutine(SelectFirstChoice());
    }

    private void HideChoices()
    {
        foreach(GameObject choiceButton in choices)
        {
            choiceButton.SetActive(false);
        }
    }

    private IEnumerator SelectFirstChoice()
    {
        //Event system cleared first before setting the event system in a different frame.
        EventSystem.current.SetSelectedGameObject(null);
        yield return new WaitForEndOfFrame();
        EventSystem.current.SetSelectedGameObject(choices[0].gameObject);
    }

    public void MakeChoice(int choiceIndex)
    {
        if (canContinueToNextLine)
        {
            currentStory.ChooseChoiceIndex(choiceIndex);
            InputManager.GetInstance().RegisterInteractPressed();
            ContinueStory();
        }     
    }

    public Ink.Runtime.Object GetVariableState(string variableName)
    {
        Ink.Runtime.Object variableValue = null;
        dialogueVariables.variables.TryGetValue(variableName, out variableValue);
        if (variableValue == null)
        {
            Debug.LogWarning("Ink Variable was found to be null: " + variableName);
        }
        return variableValue;
    }

    private bool IsLineBlank(string dialogueLine)
    {
        return dialogueLine.Trim().Equals("") || dialogueLine.Trim().Equals("\n");
    }
}

