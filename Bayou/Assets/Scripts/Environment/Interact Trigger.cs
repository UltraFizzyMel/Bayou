using Bayou.Inventory;
using Bayou.Player;
using UnityEngine;

/// <summary>
/// Stand near a locked gate and press Interact (E). Opens if the matching key flag is set
/// or the required key item is in the bag (from Landry, etc.).
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

    private bool _isOpen;

    private void Awake()
    {
        playerInRange = false;
        if (animator == null)
            animator = GetComponentInParent<Animator>();
    }

    private void Start()
    {
        if (keyGateManager == null)
            keyGateManager = KeyGateManager.Instance ?? FindFirstObjectByType<KeyGateManager>();

        // Already unlocked this session / from save later.
        if (keyGateManager != null && keyGateManager.GetFlag(keyName))
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

        var itemId = string.IsNullOrWhiteSpace(requiredItemId)
            ? KeyGateManager.GetItemIdForFlag(keyName)
            : requiredItemId;

        var unlockedByFlag = keyGateManager != null && keyGateManager.GetFlag(keyName);
        var inv = InventoryController.Instance;
        var hasItem = !string.IsNullOrWhiteSpace(itemId) && inv != null && inv.HasItemsById(itemId, 1);

        if (!unlockedByFlag && !hasItem)
        {
            Debug.Log($"[Gate] Locked — need key ({itemId ?? keyName}).");
            return;
        }

        if (hasItem && consumeKeyOnOpen && inv != null)
            inv.TryRemoveItemsById(itemId, 1);

        if (keyGateManager != null && !string.IsNullOrWhiteSpace(keyName))
            keyGateManager.SetFlag(keyName, true);

        OpenGate(playLog: true);
    }

    private void OpenGate(bool playLog)
    {
        _isOpen = true;

        if (animator != null)
            animator.SetBool("isOpen", true);

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

    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<BayouCharacterMotor>() != null)
            playerInRange = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.GetComponentInParent<BayouCharacterMotor>() != null)
            playerInRange = false;
    }
}
