using System.Collections.Generic;
using System.Reflection;
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

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
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
