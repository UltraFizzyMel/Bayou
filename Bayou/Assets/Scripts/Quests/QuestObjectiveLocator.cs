using Bayou.Fishing;
using Bayou.Fish;
using Bayou.Inventory;
using Bayou.Quests;
using UnityEngine;

/// <summary>Resolves a world position for the active quest objective / turn-in.</summary>
public static class QuestObjectiveLocator
{
    public readonly struct Objective
    {
        public readonly Vector3 WorldPosition;
        public readonly string Label;
        public readonly bool IsTurnIn;

        public Objective(Vector3 worldPosition, string label, bool isTurnIn)
        {
            WorldPosition = worldPosition;
            Label = label;
            IsTurnIn = isTurnIn;
        }
    }

    public static bool TryResolve(Quest quest, QuestManager manager, Transform player, out Objective objective)
    {
        objective = default;
        if (quest?.info == null) return false;

        var near = player != null ? player.position : Vector3.zero;
        var questId = quest.info.id;
        var turnIn = quest.state == QuestState.CAN_FINISH;

        if (turnIn)
        {
            if (QuestMarkerTarget.TryFind(questId, turnIn: true, preferredItemId: null, near, out var turnInBeacon))
            {
                objective = new Objective(turnInBeacon.MarkerWorldPosition, turnInBeacon.Label, true);
                return true;
            }

            if (TryFindNamedNpc(questId, near, out var npcPos, out var npcLabel))
            {
                objective = new Objective(npcPos, npcLabel, true);
                return true;
            }

            return false;
        }

        // Prefer the live step's own target.
        if (manager != null && manager.TryGetActiveStep(questId, out var step) &&
            step.TryGetObjectiveWorldPosition(out var stepPos, out var stepLabel))
        {
            objective = new Objective(stepPos, stepLabel, false);
            return true;
        }

        if (QuestMarkerTarget.TryFind(questId, turnIn: false, preferredItemId: null, near, out var beacon))
        {
            objective = new Objective(beacon.MarkerWorldPosition, beacon.Label, false);
            return true;
        }

        // Built-in fallbacks by quest id.
        if (TryFallbackByQuestId(questId, near, out var fallbackPos, out var fallbackLabel))
        {
            objective = new Objective(fallbackPos, fallbackLabel, false);
            return true;
        }

        return false;
    }

    private static bool TryFallbackByQuestId(string questId, Vector3 near, out Vector3 pos, out string label)
    {
        pos = default;
        label = null;
        if (string.IsNullOrWhiteSpace(questId)) return false;

        if (questId.IndexOf("Pond", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            questId.IndexOf("Shiny", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            var shiny = Object.FindFirstObjectByType<PondShinyCollectible>();
            if (shiny != null)
            {
                pos = shiny.transform.position + Vector3.up * 0.8f;
                label = "Shiny in the pond";
                return true;
            }
        }

        if (questId.IndexOf("Lantern", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            if (TryFindPickupByItemId("Item_Lantern", near, out pos, out label))
                return true;
            var byName = GameObject.Find("LanternPickup");
            if (byName != null)
            {
                pos = byName.transform.position + Vector3.up * 1.2f;
                label = "Lantern";
                return true;
            }
        }

        if (questId.IndexOf("Snapper", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            questId.IndexOf("Molly", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            questId.IndexOf("Fish", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            if (TryFindNearestNeededFish(near, out pos, out label))
                return true;
        }

        return false;
    }

    public static bool TryFindPickupByItemId(string itemId, Vector3 near, out Vector3 pos, out string label)
    {
        pos = default;
        label = null;
        QuestItemPickup best = null;
        var bestSq = float.MaxValue;
        var pickups = Object.FindObjectsByType<QuestItemPickup>(FindObjectsSortMode.None);
        for (var i = 0; i < pickups.Length; i++)
        {
            var p = pickups[i];
            if (p == null || !p.isActiveAndEnabled) continue;
            // Item field is private — match via beacon or name; also check QuestMarkerTarget on same object.
            var beacon = p.GetComponent<QuestMarkerTarget>();
            if (beacon != null && !beacon.MatchesItem(itemId))
                continue;

            var d = p.transform.position - near;
            d.y = 0f;
            var sq = d.sqrMagnitude;
            if (sq < bestSq)
            {
                bestSq = sq;
                best = p;
            }
        }

        // Prefer explicit lantern name when filtering lantern.
        if (!string.IsNullOrEmpty(itemId) &&
            itemId.IndexOf("Lantern", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            var named = GameObject.Find("LanternPickup");
            if (named != null)
            {
                pos = named.transform.position + Vector3.up * 1.2f;
                label = "Lantern";
                return true;
            }
        }

        if (best == null) return false;
        pos = best.transform.position + Vector3.up * 1.1f;
        label = best.name;
        return true;
    }

    public static bool TryFindNearestNeededFish(Vector3 near, out Vector3 pos, out string label)
    {
        pos = default;
        label = null;

        BayouFish bestFish = null;
        var bestSq = float.MaxValue;
        var fish = Object.FindObjectsByType<BayouFish>(FindObjectsSortMode.None);
        for (var i = 0; i < fish.Length; i++)
        {
            var f = fish[i];
            if (f == null || f.IsCaught || !f.isActiveAndEnabled) continue;
            var item = f.InventoryItem;
            if (item == null) continue;
            // Prefer quest fish; otherwise any catchable fish.
            var isQuestFish = item.MatchesId("Item_RedSnapper") || item.MatchesId("Item_SailfinMolly");
            if (!isQuestFish && !item.isFish) continue;

            var d = f.transform.position - near;
            d.y = 0f;
            var sq = d.sqrMagnitude;
            // Prefer snapper/molly over generic fish.
            if (!isQuestFish) sq += 2500f;
            if (sq < bestSq)
            {
                bestSq = sq;
                bestFish = f;
            }
        }

        if (bestFish != null)
        {
            pos = bestFish.transform.position + Vector3.up * 0.6f;
            label = bestFish.InventoryItem != null ? bestFish.InventoryItem.displayName : "Fish";
            return true;
        }

        // Fall back to a fishing spot.
        FishingSpot bestSpot = null;
        bestSq = float.MaxValue;
        foreach (var spot in FishingSpot.AllSpots)
        {
            if (spot == null) continue;
            var d = spot.transform.position - near;
            d.y = 0f;
            var sq = d.sqrMagnitude;
            if (sq < bestSq)
            {
                bestSq = sq;
                bestSpot = spot;
            }
        }

        if (bestSpot == null) return false;
        pos = bestSpot.transform.position + Vector3.up * 1f;
        label = bestSpot.SpotName;
        return true;
    }

    private static bool TryFindNamedNpc(string questId, Vector3 near, out Vector3 pos, out string label)
    {
        pos = default;
        label = null;

        // Caliste handles fish turn-in; Landry/Zenon handle pond + lantern.
        string[] names;
        if (questId.IndexOf("Snapper", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            questId.IndexOf("Molly", System.StringComparison.OrdinalIgnoreCase) >= 0)
            names = new[] { "Caliste", "Caliste NPC", "NPC_Caliste" };
        else
            names = new[] { "Zenon Landry", "Landry", "Father Landry", "Zenon", "NPC_Landry" };

        Transform best = null;
        var bestSq = float.MaxValue;
        for (var i = 0; i < names.Length; i++)
        {
            var go = GameObject.Find(names[i]);
            if (go == null) continue;
            var d = go.transform.position - near;
            d.y = 0f;
            var sq = d.sqrMagnitude;
            if (sq < bestSq)
            {
                bestSq = sq;
                best = go.transform;
                label = names[i];
            }
        }

        if (best == null) return false;
        pos = best.position + Vector3.up * 1.8f;
        return true;
    }
}
