using Bayou.Inventory;
using Bayou.Player;
using Bayou.UI;
using UnityEngine;

/// <summary>
/// Stand near a locked gate and press Interact (E). Opens only for this gate's key
/// (flag and/or required item id) — never another gate's key.
/// </summary>
public sealed class InteractTrigger : MonoBehaviour, IInteractionPromptSource
{
    [Tooltip("KeyGateManager flag for THIS gate only. Empty = never unlock via flag.")]
    [SerializeField] private string keyName = "";

    [Tooltip("Inventory item id for THIS gate only (e.g. Item_ChurchFoggyMarshKey).")]
    [SerializeField] private string requiredItemId;

    [Tooltip("Optional direct reference — preferred when set (avoids id/name mixups).")]
    [SerializeField] private ItemDefinition requiredKeyItem;

    [SerializeField] private bool playerInRange;
    [SerializeField] private Animator animator;
    [SerializeField] private KeyGateManager keyGateManager;
        [SerializeField] private bool consumeKeyOnOpen = true;
    [SerializeField] private Collider[] disableCollidersOnOpen;
    [SerializeField] private string openPrompt = "Open gate";
    [SerializeField] private string lockedPrompt = "Locked — need key";

    private bool _isOpen;
    private InventoryController _inv;
    private bool _subscribed;

    private void Awake()
    {
        playerInRange = false;
        if (animator == null)
            animator = GetComponentInParent<Animator>();

        if (disableCollidersOnOpen == null || disableCollidersOnOpen.Length == 0)
            AutoCollectBlockingColliders();

        if (requiredKeyItem != null && string.IsNullOrWhiteSpace(requiredItemId))
            requiredItemId = requiredKeyItem.Id;

        BindChurchFoggyMarshGate();

        // Keyless church entrances are built open. Do not mark a keyed gate used.
        if (string.IsNullOrWhiteSpace(keyName) &&
            requiredKeyItem == null &&
            string.IsNullOrWhiteSpace(requiredItemId) &&
            PassageIsClear())
        {
            _isOpen = true;
            var volume = GetComponent<Collider>();
            if (volume != null)
                volume.enabled = false;
            if (IsOpenChurchEntrance())
                ReleaseOpenChurchEntrance();
        }
    }

    /// <summary>
    /// The church gate facing the foggy marsh was left keyless. Landry's foggy key opens that gate.
    /// Caliste's maze stays on the maze key.
    /// </summary>
    private void BindChurchFoggyMarshGate()
    {
        if (!string.IsNullOrWhiteSpace(keyName) || requiredKeyItem != null || !string.IsNullOrWhiteSpace(requiredItemId))
            return;
        if (!HierarchyContains("Church")) return;
        if (!HierarchyContains("Foggy Marsh") && !HierarchyContains("East Gate")) return;
        if (HierarchyContains("Graveyard") || HierarchyContains("Maze") || HierarchyContains("Brackish"))
            return;

        keyName = KeyGateManager.FoggyMarshKeyFlag;
        requiredItemId = KeyGateManager.FoggyMarshKeyItemId;
        if (!enabled)
            enabled = true;
    }

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

    public string KeyFlagName => keyName ?? "";
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

    /// <summary>True when the player can already walk through. Those gates stay quiet.</summary>
    public bool PassageIsClear()
    {
        if (_isOpen || IsOpenChurchEntrance() || IsChurchEntranceLeftOpen() || AnimatorSaysOpen())
            return true;
        return !HasSolidBlocker();
    }

    private bool IsChurchEntranceLeftOpen()
    {
        if (!string.IsNullOrWhiteSpace(keyName) || requiredKeyItem != null)
            return false;
        if (!string.IsNullOrWhiteSpace(requiredItemId))
            return false;
        return HierarchyContains("Church Area");
    }

    private bool AnimatorSaysOpen()
    {
        if (animator == null || !animator.isActiveAndEnabled) return false;
        var parms = animator.parameters;
        for (var i = 0; i < parms.Length; i++)
        {
            if (parms[i].type == AnimatorControllerParameterType.Bool && parms[i].name == "isOpen")
                return animator.GetBool("isOpen");
        }

        return false;
    }

    private bool HasSolidBlocker()
    {
        var listed = false;
        if (disableCollidersOnOpen != null)
        {
            foreach (var col in disableCollidersOnOpen)
            {
                if (col == null || col.isTrigger) continue;
                listed = true;
                if (col.enabled) return true;
            }
        }

        if (listed) return false;

        var root = transform.parent != null ? transform.parent : transform;
        var own = GetComponent<Collider>();
        foreach (var col in root.GetComponentsInChildren<Collider>(true))
        {
            if (col == null || col == own || col.isTrigger) continue;
            if (col.enabled) return true;
        }

        return false;
    }

    private bool IsOpenChurchEntrance()
    {
        var gate = transform.parent;
        if (gate == null || gate.name != "Gate (1)")
            return false;
        var area = gate.parent;
        return area != null && area.name == "Church Area";
    }

    private void OnEnable()
    {
        TrySubscribeInventory();
        InteractionPromptBroker.Register(this);
    }

    private void OnDisable()
    {
        UnsubscribeInventory();
        InteractionPromptBroker.Unregister(this);
    }

    private void Start()
    {
        if (keyGateManager == null)
            keyGateManager = KeyGateManager.Instance ?? FindFirstObjectByType<KeyGateManager>();

        // Player builds: if the scene lost the SO reference, recover from catalog / Resources by id.
        EnsureRequiredKeyResolved();

        TrySubscribeInventory();

        // Flag is set only after this gate has been opened — restore that state.
        if (keyGateManager != null &&
            !string.IsNullOrWhiteSpace(keyName) &&
            keyGateManager.GetFlag(keyName))
            OpenGate(playLog: false);
    }

    private void EnsureRequiredKeyResolved()
    {
        if (requiredKeyItem != null)
        {
            if (string.IsNullOrWhiteSpace(requiredItemId))
                requiredItemId = requiredKeyItem.Id;
            return;
        }

        if (string.IsNullOrWhiteSpace(requiredItemId))
            requiredItemId = KeyGateManager.GetItemIdForFlag(keyName);

        if (string.IsNullOrWhiteSpace(requiredItemId))
            return;

        requiredKeyItem = ResolveKeyDefinition(requiredItemId);
        if (requiredKeyItem != null && string.IsNullOrWhiteSpace(requiredItemId))
            requiredItemId = requiredKeyItem.Id;
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

    private bool PlayerHasThisGateKey(InventoryController inv)
    {
        if (inv == null) return false;

        // Prefer stable string id — works across editor/build and duplicate SO instances.
        var itemId = ResolveItemId();
        if (!string.IsNullOrWhiteSpace(itemId) && inv.HasItemsById(itemId, 1))
            return true;

        return requiredKeyItem != null && inv.HasItems(requiredKeyItem, 1);
    }

    private string ResolveItemId()
    {
        if (!string.IsNullOrWhiteSpace(requiredItemId))
            return requiredItemId;
        if (requiredKeyItem != null)
            return requiredKeyItem.Id;
        return KeyGateManager.GetItemIdForFlag(keyName);
    }

    public bool TryGetInteractionPrompt(out InteractionPrompt prompt)
    {
        prompt = default;
        if (!playerInRange || _isOpen || PassageIsClear()) return false;

        var dist = DistToPlayerSq();
        if (CanUnlock())
        {
            prompt = new InteractionPrompt("E", openPrompt, 55, dist);
            return true;
        }

        prompt = new InteractionPrompt("E", lockedPrompt, 45, dist);
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
        if (!_subscribed)
            TrySubscribeInventory();

        if (_isOpen || !playerInRange) return;

        var dialogue = DialogueManager.GetInstance();
        if (dialogue != null && dialogue.dialogueIsPlaying)
            return;

        var input = InputManager.GetInstance();
        if (input == null || !input.GetInteractPressed())
            return;

        TryUnlock();
    }

    public void TryUnlock()
    {
        if (_isOpen) return;

        if (keyGateManager == null)
            keyGateManager = KeyGateManager.Instance ?? FindFirstObjectByType<KeyGateManager>();

        if (!CanUnlock())
        {
            Debug.Log($"[Gate] Locked — need '{ResolveItemId() ?? keyName}' (this gate only).");
            return;
        }

        var itemIdForConsume = ResolveItemId();
        var inv = InventoryController.Instance;
        var hasItem = PlayerHasThisGateKey(inv);

        if (hasItem && consumeKeyOnOpen && inv != null && !string.IsNullOrWhiteSpace(itemIdForConsume))
            inv.TryRemoveItemsById(itemIdForConsume, 1);

        if (keyGateManager != null && !string.IsNullOrWhiteSpace(keyName))
            keyGateManager.SetFlag(keyName, true);

        OpenGate(playLog: true);
    }

    /// <summary>Kept for callers after buying a key. Gates stay closed until Interact (E).</summary>
    public void TryAutoOpenIfUnlocked()
    {
        // Intentional no-op: obtaining a key unlocks the gate, it does not swing it open.
    }

    private bool CanUnlock()
    {
        if (keyGateManager == null)
            keyGateManager = KeyGateManager.Instance ?? FindFirstObjectByType<KeyGateManager>();

        // Empty keyName + empty item = decorative / never unlockable.
        var itemId = ResolveItemId();
        if (string.IsNullOrWhiteSpace(keyName) && string.IsNullOrWhiteSpace(itemId) && requiredKeyItem == null)
            return false;

        return PlayerHasThisGateKey(InventoryController.Instance);
    }

    private void OpenGate(bool playLog)
    {
        _isOpen = true;

        if (animator != null)
        {
            animator.SetBool("isOpen", true);
            animator.CrossFade("Open", 0.05f, 0, 0f);
        }

        if (disableCollidersOnOpen != null)
        {
            foreach (var col in disableCollidersOnOpen)
            {
                if (col != null)
                    col.enabled = false;
            }
        }

        if (playLog)
            Debug.Log($"[Gate] Opened ({keyName} / {ResolveItemId()}).");
    }

    private void AutoCollectBlockingColliders()
    {
        var root = GetComponentInParent<Animator>() != null
            ? GetComponentInParent<Animator>().transform
            : transform.root;
        var found = new System.Collections.Generic.List<Collider>();
        foreach (var col in root.GetComponentsInChildren<Collider>(true))
        {
            if (col == null || col.isTrigger) continue;
            found.Add(col);
        }

        disableCollidersOnOpen = found.ToArray();
    }

    private void TrySubscribeInventory()
    {
        var inv = InventoryController.Instance ?? FindFirstObjectByType<InventoryController>();
        if (inv == null || _subscribed) return;
        _inv = inv;
        _inv.InventoryChanged += OnInventoryChanged;
        _subscribed = true;
    }

    private void UnsubscribeInventory()
    {
        if (_inv != null && _subscribed)
            _inv.InventoryChanged -= OnInventoryChanged;
        _subscribed = false;
        _inv = null;
    }

    private void OnInventoryChanged()
    {
        // Key pickup / shop buy only grants unlock — player still presses E at the gate.
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
