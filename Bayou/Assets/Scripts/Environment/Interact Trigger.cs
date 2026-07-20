using Bayou.Inventory;
using Bayou.Player;
using UnityEngine;

/// <summary>
/// Stand near a locked gate and press Interact (E). Opens if the matching key flag is set
/// or the required key item is in the bag (from Landry / Caliste, etc.).
/// </summary>
public sealed class InteractTrigger : MonoBehaviour
{
    [SerializeField] private string keyName = "hasKeyChurchToGraveyard";
    [Tooltip("Inventory item asset name. Leave empty to use KeyGateManager mapping for keyName.")]
    [SerializeField] private string requiredItemId;
    [SerializeField] private bool playerInRange;
    [SerializeField] private Animator animator;
    [SerializeField] private KeyGateManager keyGateManager;
    [SerializeField] private bool consumeKeyOnOpen;
    [SerializeField] private Collider[] disableCollidersOnOpen;
    [Tooltip("If the player already has the key when entering the trigger, open without waiting for E.")]
    [SerializeField] private bool autoOpenWhenUnlocked = true;

    private bool _isOpen;

    private void Awake()
    {
        playerInRange = false;
        if (animator == null)
            animator = GetComponentInParent<Animator>();

        // Prefab may leave this empty — collect solid blockers under the gate root.
        if (disableCollidersOnOpen == null || disableCollidersOnOpen.Length == 0)
            AutoCollectBlockingColliders();
    }

    private void Start()
    {
        if (keyGateManager == null)
            keyGateManager = KeyGateManager.Instance ?? FindFirstObjectByType<KeyGateManager>();

        if (CanUnlock())
            OpenGate(playLog: false);
    }

    private void Update()
    {
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
            Debug.Log($"[Gate] Locked — need key ({ResolveItemId() ?? keyName}).");
            return;
        }

        var itemIdForConsume = ResolveItemId();
        var inv = InventoryController.Instance;
        var hasItem = !string.IsNullOrWhiteSpace(itemIdForConsume) &&
                      inv != null &&
                      inv.HasItemsById(itemIdForConsume, 1);

        if (hasItem && consumeKeyOnOpen && inv != null)
            inv.TryRemoveItemsById(itemIdForConsume, 1);

        if (keyGateManager != null && !string.IsNullOrWhiteSpace(keyName))
            keyGateManager.SetFlag(keyName, true);

        OpenGate(playLog: true);
    }

    private bool CanUnlock()
    {
        if (keyGateManager == null)
            keyGateManager = KeyGateManager.Instance ?? FindFirstObjectByType<KeyGateManager>();

        var itemId = ResolveItemId();
        var unlockedByFlag = keyGateManager != null && keyGateManager.GetFlag(keyName);
        var inv = InventoryController.Instance;
        var hasItem = !string.IsNullOrWhiteSpace(itemId) && inv != null && inv.HasItemsById(itemId, 1);
        return unlockedByFlag || hasItem;
    }

    private string ResolveItemId() =>
        string.IsNullOrWhiteSpace(requiredItemId)
            ? KeyGateManager.GetItemIdForFlag(keyName)
            : requiredItemId;

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
            Debug.Log($"[Gate] Opened ({keyName}).");
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
