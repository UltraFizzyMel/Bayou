using Bayou.Inventory;
using Bayou.Player;
using UnityEngine;

/// <summary>
/// Stand near a locked gate and press Interact (E). Opens only for this gate's key
/// (flag and/or required item id) — never another gate's key.
/// </summary>
public sealed class InteractTrigger : MonoBehaviour
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
    [SerializeField] private bool consumeKeyOnOpen;
    [SerializeField] private Collider[] disableCollidersOnOpen;
    [Tooltip("If the player already has the key when entering the trigger, open without waiting for E.")]
    [SerializeField] private bool autoOpenWhenUnlocked = true;

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

        // Prefer the ScriptableObject's stable id when assigned.
        if (requiredKeyItem != null && string.IsNullOrWhiteSpace(requiredItemId))
            requiredItemId = requiredKeyItem.Id;
    }

    private void Start()
    {
        if (keyGateManager == null)
            keyGateManager = KeyGateManager.Instance ?? FindFirstObjectByType<KeyGateManager>();

        // Player builds: if the scene lost the SO reference, recover from catalog / Resources by id.
        EnsureRequiredKeyResolved();

        TrySubscribeInventory();
        if (CanUnlock())
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

    private void OnEnable() => TrySubscribeInventory();

    private void OnDisable() => UnsubscribeInventory();

    private void Update()
    {
        if (!_subscribed)
            TrySubscribeInventory();

        if (_isOpen || !playerInRange) return;

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

    /// <summary>Called after buying a key / syncing flags — opens if unlocked and auto-open is on.</summary>
    public void TryAutoOpenIfUnlocked()
    {
        if (_isOpen) return;
        if (!CanUnlock()) return;
        if (autoOpenWhenUnlocked || playerInRange)
            TryUnlock();
    }

    private bool CanUnlock()
    {
        if (keyGateManager == null)
            keyGateManager = KeyGateManager.Instance ?? FindFirstObjectByType<KeyGateManager>();

        // Empty keyName + empty item = decorative / never unlockable.
        var itemId = ResolveItemId();
        if (string.IsNullOrWhiteSpace(keyName) && string.IsNullOrWhiteSpace(itemId) && requiredKeyItem == null)
            return false;

        var unlockedByFlag = !string.IsNullOrWhiteSpace(keyName) &&
                             keyGateManager != null &&
                             keyGateManager.GetFlag(keyName);

        var hasItem = PlayerHasThisGateKey(InventoryController.Instance);
        return unlockedByFlag || hasItem;
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
        if (_isOpen) return;
        if (autoOpenWhenUnlocked && CanUnlock())
            TryUnlock();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayer(other)) return;

        playerInRange = true;
        if (autoOpenWhenUnlocked && !_isOpen && CanUnlock())
            TryUnlock();
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
