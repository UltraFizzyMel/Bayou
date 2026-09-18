using System;
using System.Collections.Generic;

/// <summary>
/// Session flags for discovery, shop unlocks, and fast-travel. Set from Ink via SetWorldFlag.
/// </summary>
public static class WorldFlags
{
    public const string FoundCalisteFoggyMarsh = "found_caliste_foggy_marsh";
    public const string FoundCalisteBrackishShore = "found_caliste_brackish_shore";
    public const string FoundSabineFoggyMarsh = "found_sabine_foggy_marsh";
    public const string FoundSabineBrackishShore = "found_sabine_brackish_shore";
    public const string FoundBrackishHouse1 = "found_brackish_house_1";
    public const string FoundBrackishHouse2 = "found_brackish_house_2";
    public const string FoundBoxGraveyard = "found_box_graveyard";
    public const string FoundBoxFoggyMarsh = "found_box_foggy_marsh";
    public const string FoundBoxBrackishShore = "found_box_brackish_shore";
    public const string ReturnedAccordion = "returned_accordion";
    public const string DisposedAccordion = "disposed_accordion";
    public const string FastTravelChurchFoggy = "fast_travel_church_foggy";
    public const string FastTravelBrackish = "fast_travel_brackish";
    public const string FastTravelFree = "fast_travel_free";
    public const string FastTravelCostTwo = "fast_travel_cost_two";
    public const string ShopNetUpgrade = "shop_net_upgrade";
    public const string ShopUpgradedRod = "shop_upgraded_rod";
    public const string ShopWadingBoots = "shop_wading_boots";
    public const string ShopStraps = "shop_straps";
    public const string ShopSatchel = "shop_satchel";

    private static readonly HashSet<string> Flags = new(StringComparer.OrdinalIgnoreCase);

    public static void Set(string flagName, bool value = true)
    {
        if (string.IsNullOrWhiteSpace(flagName)) return;
        flagName = flagName.Trim();
        if (value) Flags.Add(flagName);
        else Flags.Remove(flagName);
        ApplyKnown(flagName, value);
    }

        public static bool Has(string flagName)
        {
            if (string.IsNullOrWhiteSpace(flagName)) return false;
            return Flags.Contains(flagName.Trim());
        }

    public static void ClearAll()
    {
        Flags.Clear();
        FastTravelState.Reset();
    }

    private static void ApplyKnown(string flag, bool value)
    {
        switch (flag)
        {
            case FastTravelChurchFoggy:
                FastTravelState.ChurchToFoggyMarsh = value;
                break;
            case FastTravelBrackish:
                FastTravelState.ToBrackishShore = value;
                break;
            case FastTravelFree:
                FastTravelState.Free = value;
                if (value) FastTravelState.CostInFish = 0;
                break;
            case FastTravelCostTwo:
                FastTravelState.CostInFish = value ? 2 : 1;
                FastTravelState.Free = false;
                break;
            case FoundCalisteFoggyMarsh:
                if (value) Set(ShopNetUpgrade, true);
                break;
            case FoundCalisteBrackishShore:
                if (value) Set(ShopUpgradedRod, true);
                break;
            case FoundBoxGraveyard:
                if (value) Set(ShopWadingBoots, true);
                break;
            case FoundBoxFoggyMarsh:
                if (value) Set(ShopStraps, true);
                break;
            case FoundBoxBrackishShore:
                if (value) Set(ShopSatchel, true);
                break;
        }
    }
}

/// <summary>Boat fast-travel unlocks granted by Marie / Olivier quests.</summary>
public static class FastTravelState
{
    public static bool ChurchToFoggyMarsh;
    public static bool ToBrackishShore;
    public static bool Free;
    public static int CostInFish = 1;

    public static void Reset()
    {
        ChurchToFoggyMarsh = false;
        ToBrackishShore = false;
        Free = false;
        CostInFish = 1;
    }
}
