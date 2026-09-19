using Bayou.Inventory;
using Bayou.Save;
using UnityEngine;

/// <summary>
/// Flowchart rewards applied when a quest finishes. Dialogue is left unchanged —
/// Ink may also grant the same items; grants are skipped if already owned.
/// </summary>
public static class QuestRewards
{
    public readonly struct Spec
    {
        public readonly (string id, int count)[] Consume;
        public readonly string[] GrantItems;
        public readonly string[] KeyFlags;
        public readonly string[] WorldFlagsToSet;

        public Spec(
            (string id, int count)[] consume = null,
            string[] grantItems = null,
            string[] keyFlags = null,
            string[] worldFlags = null)
        {
            Consume = consume;
            GrantItems = grantItems;
            KeyFlags = keyFlags;
            WorldFlagsToSet = worldFlags;
        }
    }

    public static void Apply(Quest quest)
    {
        if (quest?.info == null) return;
        if (!TryGet(quest.info.id, out var spec)) return;

        var inv = InventoryController.Instance ?? Object.FindFirstObjectByType<InventoryController>();

        if (spec.Consume != null && inv != null)
        {
            foreach (var (id, count) in spec.Consume)
                inv.TryRemoveItemsById(id, count);
        }

        if (spec.GrantItems != null)
        {
            foreach (var itemId in spec.GrantItems)
                GrantItem(itemId);
        }

        if (spec.KeyFlags != null)
        {
            var keys = KeyGateManager.Instance ?? Object.FindFirstObjectByType<KeyGateManager>();
            if (keys != null)
            {
                foreach (var flag in spec.KeyFlags)
                    keys.GrantKeyFlag(flag);
            }
        }

        if (spec.WorldFlagsToSet != null)
        {
            foreach (var flag in spec.WorldFlagsToSet)
                WorldFlags.Set(flag, true);
        }
    }

    private static bool TryGet(string questId, out Spec spec)
    {
        spec = default;
        switch (questId)
        {
            case QuestIds.CollectLantern:
                // Landry's dialogue turns this in. The foggy marsh key is bought from Caliste.
                return false;
            case QuestIds.SnapperAndMolly:
                // Caliste's shop sells the maze key after she trusts you.
                return false;
            case QuestIds.BreakTheSeals:
                spec = new Spec(grantItems: new[] { "Item_SpiritLantern" });
                return true;
            case QuestIds.EmergencyDinner:
                spec = new Spec(
                    consume: new[] { ("Item_SpottedGar", 2) },
                    grantItems: new[] { "Item_FoggyMarshGraveyardKey" },
                    keyFlags: new[] { KeyGateManager.FoggyMarshGraveyardKeyFlag });
                return true;
            case QuestIds.PaymentForARide:
                spec = new Spec(
                    consume: new[] { ("Item_ChannelCatfish", 2), ("Item_Flounder", 1) },
                    worldFlags: new[] { WorldFlags.FastTravelChurchFoggy });
                return true;
            case QuestIds.ExpandedHorizons:
                spec = new Spec(
                    consume: new[] { ("Item_BlueCrab", 2), ("Item_Crawfish", 2) },
                    worldFlags: new[] { WorldFlags.FastTravelBrackish });
                return true;
            case QuestIds.LostAccordion:
                spec = new Spec(
                    consume: new[] { ("Item_Accordion", 1) },
                    grantItems: new[] { "Item_Satchel" });
                return true;
            case QuestIds.MealToDieForOlivier:
                spec = new Spec(
                    consume: new[] { ("Item_ElectricEel", 3) },
                    worldFlags: new[] { WorldFlags.FastTravelFree });
                return true;
            case QuestIds.MealToDieForMarie:
                spec = new Spec(
                    consume: new[] { ("Item_Pufferfish", 3) },
                    worldFlags: new[] { WorldFlags.FastTravelFree });
                return true;
            case QuestIds.CatfishAndGar:
                spec = new Spec(
                    consume: new[] { ("Item_AlligatorGar", 1), ("Item_ChannelCatfish", 1) },
                    worldFlags: new[] { WorldFlags.FastTravelCostTwo });
                return true;
            case QuestIds.LostCargo:
                spec = new Spec(consume: new[]
                {
                    ("Item_CalisteCargoGraveyard", 1),
                    ("Item_CalisteCargoFoggy", 1),
                    ("Item_CalisteCargoBrackish", 1)
                });
                return true;
            case QuestIds.SearchForHerbalist:
                spec = new Spec(
                    consume: new[] { ("Item_HerbalistFlower", 1) },
                    grantItems: new[] { "Item_Satchel" });
                return true;
            case QuestIds.SharedDinner:
                spec = new Spec(
                    consume: new[]
                    {
                        ("Item_LargemouthBass", 1),
                        ("Item_BlueCrab", 1),
                        ("Item_Flounder", 1)
                    },
                    grantItems: new[] { "Item_ChurchReturnKey" },
                    keyFlags: new[] { KeyGateManager.ChurchReturnKeyFlag });
                return true;
            case QuestIds.SpecialDelivery:
                spec = new Spec(
                    consume: new[] { ("Item_RedSnapper", 1), ("Item_SailfinMolly", 1) },
                    worldFlags: new[] { "lantern_radius_upgrade" });
                return true;
            case QuestIds.VisitGraves:
                spec = new Spec(
                    grantItems: new[] { "Item_LandryTombKey" },
                    keyFlags: new[] { KeyGateManager.LandryTombKeyFlag });
                return true;
            case QuestIds.LostSupplies:
                spec = new Spec(
                    consume: new[] { ("Item_SabineSupplies", 1) },
                    worldFlags: new[] { "herbal_knowledge" });
                return true;
            case QuestIds.RitualComponents:
                spec = new Spec(
                    consume: new[]
                    {
                        ("Item_LargemouthBass", 1),
                        ("Item_AlligatorGar", 1),
                        ("Item_BlueCatfish", 1)
                    },
                    grantItems: new[] { "Item_SpiritLantern" });
                return true;
            default:
                return false;
        }
    }

    private static void GrantItem(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId)) return;
        var inv = InventoryController.Instance ?? Object.FindFirstObjectByType<InventoryController>();
        if (inv == null) return;
        if (inv.HasItemsById(itemId, 1)) return;

        var def = ResolveItem(itemId);
        if (def == null)
        {
            Debug.LogWarning($"[QuestRewards] Unknown item '{itemId}'.");
            return;
        }

        var dialogue = DialogueManager.GetInstance();
        if (dialogue != null && dialogue.dialogueIsPlaying)
        {
            dialogue.QueueReceivedItem(def);
            return;
        }

        CaughtFishPresenter.Present(def, "You've received");
    }

    private static ItemDefinition ResolveItem(string itemId)
    {
        var catalog = GameSaveSystem.Instance != null ? GameSaveSystem.Instance.ItemCatalog : null;
        if (catalog == null)
            catalog = Resources.Load<ItemCatalog>("Bayou/ItemCatalog");
        var fromCatalog = catalog != null ? catalog.Resolve(itemId) : null;
        if (fromCatalog != null) return fromCatalog;
        return Resources.Load<ItemDefinition>($"Bayou/Items/{itemId}");
    }
}
