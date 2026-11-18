using Bayou.Inventory;
using Bayou.Player;
using Bayou.UI;
using UnityEngine;

/// <summary>
/// Stand near a locked gate and press Interact (E). Opens only for this gate's key
/// (flag and/or required item id) — never another gate's key.
/// </summary>
public sealed class DrawBridge : MonoBehaviour, IInteractionPromptSource
{


    [SerializeField] private bool playerInRange;
    [SerializeField] private Animator animator;
    [SerializeField] public GameObject disableColliderOnOpen;
    [SerializeField] private string openPrompt = "Lower Bridge";
  

    private bool _isOpen;
    private bool _subscribed;

    private void Awake()
    {
        playerInRange = false;
        if (animator == null)
            animator = GetComponentInParent<Animator>();

       // if (disableCollidersOnOpen == null || disableCollidersOnOpen.Length == 0)
        //AutoCollectBlockingColliders();
    }

    /// <summary>
    /// The church gate facing the foggy marsh was left keyless. Landry's foggy key opens that gate.
    /// Caliste's maze stays on the maze key.
    /// </summary>
  

    private void ReleaseOpenChurchEntrance()
    {
        var root = transform.parent != null ? transform.parent : transform;
        var cols = root.GetComponentsInChildren<Collider>(true);
        for (var i = 0; i < cols.Length; i++)
        {
            var col = cols[i];
            if (col == null || col.isTrigger) continue;
            col.enabled = false;
        }

        if (animator == null) return;
        if (animator.transform != root && !animator.transform.IsChildOf(root)) return;
        var parms = animator.parameters;
        for (var i = 0; i < parms.Length; i++)
        {
            if (parms[i].type != AnimatorControllerParameterType.Bool || parms[i].name != "isOpen")
                continue;
            animator.SetBool("isOpen", true);
            break;
        }
    }

    private static InteractTrigger[] _cachedGates;
    private static float _gateCacheUntil;

    public static InteractTrigger[] AllGates()
    {
        if (_cachedGates == null || Time.unscaledTime >= _gateCacheUntil)
        {
            _cachedGates = FindObjectsByType<InteractTrigger>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            _gateCacheUntil = Time.unscaledTime + 0.75f;
        }

        return _cachedGates;
    }

    public bool HasBeenOpened => _isOpen;


    public bool HierarchyContains(string fragment)
    {
        if (string.IsNullOrWhiteSpace(fragment)) return false;
        for (var p = transform; p != null; p = p.parent)
        {
            if (p.name.IndexOf(fragment, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }

        return false;
    }

   

    private void OnEnable()
    {
        InteractionPromptBroker.Register(this);
    }

    private void OnDisable()
    {
        InteractionPromptBroker.Unregister(this);
    }

 
    

    private static ItemDefinition ResolveKeyDefinition(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId)) return null;

        var catalog = Bayou.Save.GameSaveSystem.Instance != null
            ? Bayou.Save.GameSaveSystem.Instance.ItemCatalog
            : null;
        var fromCatalog = catalog != null ? catalog.Resolve(itemId) : null;
        if (fromCatalog != null) return fromCatalog;

        var fromResources = Resources.Load<ItemDefinition>($"Bayou/Items/{itemId}");
        if (fromResources != null) return fromResources;

        foreach (var def in Resources.LoadAll<ItemDefinition>("Bayou/Items"))
        {
            if (def != null && def.MatchesId(itemId))
                return def;
        }

        return null;
    }

    

    public bool TryGetInteractionPrompt(out InteractionPrompt prompt)
    {
        prompt = default;
        if (!playerInRange || _isOpen) return false;

        var dist = DistToPlayerSq();
  
            prompt = new InteractionPrompt("E", openPrompt, 55, dist);
            return true;

    }

    private float DistToPlayerSq()
    {
        var p = PlayerLocator.Transform;
        if (p == null) return 0f;
        var d = transform.position - p.position;
        d.y = 0f;
        return d.sqrMagnitude;
    }

    private void Update()
    {

        if (_isOpen || !playerInRange) return;
        if (Bayou.World.BrambleBurn.TookInteractThisFrame) return;

        var dialogue = DialogueManager.GetInstance();
        if (dialogue != null && dialogue.dialogueIsPlaying)
            return;
        if (DialogueTrigger.HasTalkAtMost(DistToPlayerSq()))
            return;

        var input = InputManager.GetInstance();
        if (input == null || !input.GetInteractPressed())
            return;

        LowerBridge();
    }

   
  

    private void LowerBridge()
    {

        if (animator != null)
        {
            animator.SetBool("isOpen", true);
            animator.CrossFade("Open", 0.05f, 0, 0f);
            _isOpen = true;
            disableColliderOnOpen.SetActive(false);
        }

  

    }

    

   

    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayer(other)) return;
        playerInRange = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (IsPlayer(other))
            playerInRange = false;
    }

    private static bool IsPlayer(Collider other) =>
        other != null &&
        (other.CompareTag("Player") || other.GetComponentInParent<BayouCharacterMotor>() != null);
}
