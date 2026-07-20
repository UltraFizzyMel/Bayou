using System.Collections.Generic;
using Bayou.Inventory;
using UnityEngine;

/// <summary>
/// Tracks which keys the player has earned and maps flag names → inventory item ids.
/// Uses explicit flag access (no reflection) so IL2CPP player builds keep working.
/// </summary>
public sealed class KeyGateManager : MonoBehaviour
{
    public const string FoggyMarshKeyFlag = "hasKeyChurchToFoggyMarsh";
    public const string FoggyMarshKeyItemId = "Item_ChurchFoggyMarshKey";
    public const string GraveyardKeyFlag = "hasKeyChurchToGraveyard";
    public const string GraveyardKeyItemId = "Item_ChurchGraveyardKey";

    public static KeyGateManager Instance { get; private set; }

    public bool hasKeyChurchToGraveyard;
    public bool hasKeyChurchToFoggyMarsh;
    public bool hasKeyChurchToBrackishShore;
    public bool hasKeyGraveyardOne;
    public bool hasKeyGraveyardTwo;

    private static readonly Dictionary<string, string> FlagToItemId = new()
    {
        { GraveyardKeyFlag, GraveyardKeyItemId },
        { FoggyMarshKeyFlag, FoggyMarshKeyItemId },
        { "hasKeyChurchToBrackishShore", "Item_ChurchBrackishShoreKey" },
        { "hasKeyGraveyardOne", "Item_GraveyardKeyOne" },
        { "hasKeyGraveyardTwo", "Item_GraveyardKeyTwo" }
    };

    private InventoryController _inv;
    private bool _subscribed;

    private void Awake()
    {
        Instance = this;
    }

    private void OnEnable() => TrySubscribe();

    private void Start() => SyncKeysFromInventory();

    private void Update()
    {
        if (!_subscribed)
            TrySubscribe();
    }

    private void OnDestroy()
    {
        Unsubscribe();
        if (Instance == this)
            Instance = null;
    }

    private void TrySubscribe()
    {
        var inv = InventoryController.Instance ?? FindFirstObjectByType<InventoryController>();
        if (inv == null || _subscribed) return;
        _inv = inv;
        _inv.InventoryChanged += OnInventoryChanged;
        _subscribed = true;
        SyncKeysFromInventory();
    }

    private void Unsubscribe()
    {
        if (_inv != null && _subscribed)
            _inv.InventoryChanged -= OnInventoryChanged;
        _subscribed = false;
        _inv = null;
    }

    private void OnInventoryChanged() => SyncKeysFromInventory();

    /// <summary>Sets key flags for any matching key items currently in the bag (e.g. bought from Caliste).</summary>
    public void SyncKeysFromInventory()
    {
        var inv = _inv ?? InventoryController.Instance;
        if (inv == null) return;

        foreach (var pair in FlagToItemId)
        {
            if (inv.HasItemsById(pair.Value, 1))
                SetFlag(pair.Key, true);
        }

        // Buying a key while standing at the gate (or after Close Deal) should open it.
        TryOpenReadyGates();
    }

    /// <summary>Asks every gate to open if its key/flag is now satisfied.</summary>
    public void TryOpenReadyGates()
    {
        var gates = FindObjectsByType<InteractTrigger>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (var i = 0; i < gates.Length; i++)
            gates[i]?.TryAutoOpenIfUnlocked();
    }

    public static string GetItemIdForFlag(string flagName)
    {
        if (string.IsNullOrWhiteSpace(flagName)) return null;
        return FlagToItemId.TryGetValue(flagName, out var id) ? id : null;
    }

    public bool GetFlag(string flagName)
    {
        if (string.IsNullOrWhiteSpace(flagName)) return false;
        return flagName switch
        {
            GraveyardKeyFlag => hasKeyChurchToGraveyard,
            FoggyMarshKeyFlag => hasKeyChurchToFoggyMarsh,
            "hasKeyChurchToBrackishShore" => hasKeyChurchToBrackishShore,
            "hasKeyGraveyardOne" => hasKeyGraveyardOne,
            "hasKeyGraveyardTwo" => hasKeyGraveyardTwo,
            _ => false
        };
    }

    public void SetFlag(string flagName, bool value)
    {
        if (string.IsNullOrWhiteSpace(flagName)) return;
        switch (flagName)
        {
            case GraveyardKeyFlag:
                hasKeyChurchToGraveyard = value;
                break;
            case FoggyMarshKeyFlag:
                hasKeyChurchToFoggyMarsh = value;
                break;
            case "hasKeyChurchToBrackishShore":
                hasKeyChurchToBrackishShore = value;
                break;
            case "hasKeyGraveyardOne":
                hasKeyGraveyardOne = value;
                break;
            case "hasKeyGraveyardTwo":
                hasKeyGraveyardTwo = value;
                break;
            default:
                Debug.LogWarning($"[KeyGate] Unknown flag '{flagName}'.");
                break;
        }
    }

    /// <summary>Marks the key flag and opens any ready gates.</summary>
    public void GrantKeyFlag(string flagName)
    {
        SetFlag(flagName, true);
        TryOpenReadyGates();
    }
}
