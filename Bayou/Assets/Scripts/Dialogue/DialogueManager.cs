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

    private bool canContinueToNextLine = false;

    private Coroutine displayLineCoroutine;

    private static DialogueManager instance;

    private const string SPEAKER_TAG = "speaker";
    private const string PORTRAIT_TAG = "portrait";
    private const string LAYOUT_TAG = "layout";
    private const string BACKGROUND_TAG = "background";

    private DialogueVariables dialogueVariables;

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

    public void Update()
    {
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

    private IEnumerator  ExitDialogueMode()
    {
        yield return new WaitForSeconds(0.2f);

        dialogueVariables.StopListening(currentStory);

        dialogueIsPlaying = false;
        dialoguePanel.SetActive(false);
        dialogueText.text = "";
    }

    private void ContinueStory()
    {
        if (currentStory.canContinue)
        {
            //set Text for the current dialogue line
            if (displayLineCoroutine != null)
            {
                StopCoroutine(displayLineCoroutine);
            }
            displayLineCoroutine = StartCoroutine(DisplayLine(currentStory.Continue()));

            //handle tags
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
            choicesText[index].text = choice.text;
            index++;
        }
        //Go through the remaining choices the UI supports and make sure they're hidden
        for (int i = index; i < choices.Length; i++)
        {
            choices[i].gameObject.SetActive(false);
        }

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
}
