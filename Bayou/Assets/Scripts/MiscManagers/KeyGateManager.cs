using System.Collections.Generic;
using System.Reflection;
using Bayou.Inventory;
using UnityEngine;

/// <summary>
/// Tracks which keys the player has earned and maps flag names → inventory item ids.
/// </summary>
public sealed class KeyGateManager : MonoBehaviour
{
    public static KeyGateManager Instance { get; private set; }

    public bool hasKeyChurchToGraveyard;
    public bool hasKeyChurchToFoggyMarsh;
    public bool hasKeyChurchToBrackishShore;
    public bool hasKeyGraveyardOne;
    public bool hasKeyGraveyardTwo;

    private static readonly Dictionary<string, string> FlagToItemId = new()
    {
        { "hasKeyChurchToGraveyard", "Item_ChurchGraveyardKey" },
        { "hasKeyChurchToFoggyMarsh", "Item_ChurchFoggyMarshKey" },
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
    }

    public static string GetItemIdForFlag(string flagName)
    {
        if (string.IsNullOrWhiteSpace(flagName)) return null;
        return FlagToItemId.TryGetValue(flagName, out var id) ? id : null;
    }

    public bool GetFlag(string flagName)
    {
        if (string.IsNullOrWhiteSpace(flagName)) return false;
        var field = GetType().GetField(flagName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (field == null || field.FieldType != typeof(bool)) return false;
        return (bool)field.GetValue(this);
    }

    public void SetFlag(string flagName, bool value)
    {
        if (string.IsNullOrWhiteSpace(flagName)) return;
        var field = GetType().GetField(flagName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (field == null || field.FieldType != typeof(bool))
        {
            Debug.LogWarning($"[KeyGate] Unknown flag '{flagName}'.");
            return;
        }

        field.SetValue(this, value);
    }

    /// <summary>Marks the key flag and (optionally) syncs from inventory presence.</summary>
    public void GrantKeyFlag(string flagName)
    {
        SetFlag(flagName, true);
    }
}
