using System.Collections.Generic;
using Bayou.Inventory;
using Bayou.Save;
using UnityEngine;

namespace Bayou.Fishing
{
    /// <summary>
    /// Creates MovementTest fishing spots at play start when none exist in the scene.
    /// Layout: Tutorial / Church / Graveyard entrance / tree pond / Caliste pond.
    /// </summary>
    public static class FishingSpotBootstrap
    {
        private const string RootName = "FishingSpots";

        private readonly struct WaterAnchor
        {
            public readonly Vector3 Position;
            public readonly Collider Collider;

            public WaterAnchor(Vector3 position, Collider collider)
            {
                Position = position;
                Collider = collider;
            }
        }

        public static void EnsureInScene()
        {
            if (Object.FindFirstObjectByType<FishingSpot>() != null)
                return;
            if (GameObject.Find(RootName) != null)
                return;

            var catalog = GameSaveSystem.Instance != null ? GameSaveSystem.Instance.ItemCatalog : null;
            var snapper = Resolve(catalog, "Item_RedSnapper");
            var molly = Resolve(catalog, "Item_SailfinMolly");
            var catfish = Resolve(catalog, "Item_ChannelCatfish");
            var rosary = Resolve(catalog, "Item_RosaryNecklace");

            if (snapper == null || molly == null || catfish == null || rosary == null)
            {
                Debug.LogWarning("[FishingSpots] Missing fish/rosary items in ItemCatalog — spots not created.");
                return;
            }

            // Replace the old lone church fish (generic Item_Fish, no tool rules).
            var legacy = GameObject.Find("Fish");
            if (legacy != null && legacy.GetComponent<FishingSpot>() == null)
                legacy.SetActive(false);

            var root = new GameObject(RootName);

            // TUTORIAL — 2 Red Snappers (NET)
            CreateSpot(root.transform, "Tutorial_Net_Snappers",
                ResolveWater("Water (1)", new Vector3(3f, 0.15f, -86f), "Tutorial Area"),
                7f, FishCatchTool.Net,
                new List<FishingSpot.FishSpawn>
                {
                    Fish(snapper, FishCatchTool.Net, 2, true, new Color(0.9f, 0.25f, 0.2f))
                });

            // CHURCH — 2 Red Snappers (NET) + one-time Rosary (NET)
            CreateSpot(root.transform, "Church_Net",
                ResolveWater("Water", new Vector3(-22f, 0.15f, -43.5f), null),
                9f, FishCatchTool.Net,
                new List<FishingSpot.FishSpawn>
                {
                    Fish(snapper, FishCatchTool.Net, 2, true, new Color(0.9f, 0.25f, 0.2f))
                },
                new List<FishingSpot.LootSpawn>
                {
                    Loot(rosary, new Color(0.95f, 0.85f, 0.3f), new Vector3(1.2f, 0f, -0.8f))
                });

            // GRAVEYARD — small entrance: 1 Red Snapper (NET)
            CreateSpot(root.transform, "Graveyard_Entrance_Net",
                ResolveWater("Water (4)", new Vector3(-100f, 0.15f, 100f), "Graveyard Water Sources"),
                5f, FishCatchTool.Net,
                new List<FishingSpot.FishSpawn>
                {
                    Fish(snapper, FishCatchTool.Net, 1, true, new Color(0.9f, 0.25f, 0.2f))
                });

            // GRAVEYARD — big pond bottom-left (tree): 2 Molly NET + 1 Catfish ROD
            CreateSpot(root.transform, "Graveyard_TreePond",
                ResolveWater("Water (1)", new Vector3(-68f, 0.15f, 47f), "Graveyard Water Sources"),
                10f, FishCatchTool.Net,
                new List<FishingSpot.FishSpawn>
                {
                    Fish(molly, FishCatchTool.Net, 2, true, new Color(0.35f, 0.65f, 0.95f)),
                    Fish(catfish, FishCatchTool.Rod, 1, true, new Color(0.25f, 0.25f, 0.3f))
                });

            // Big pond next to Caliste — 2 Sailfin Molly (NET)
            CreateSpot(root.transform, "CalistePond_Net",
                ResolveWater("Water (3)", new Vector3(-70f, 0.15f, 92f), "Graveyard Water Sources"),
                9f, FishCatchTool.Net,
                new List<FishingSpot.FishSpawn>
                {
                    Fish(molly, FishCatchTool.Net, 2, true, new Color(0.35f, 0.65f, 0.95f))
                });

            Debug.Log("[FishingSpots] Created Tutorial / Church / Graveyard / Caliste spots.");
        }

        private static ItemDefinition Resolve(ItemCatalog catalog, string id)
        {
            if (catalog != null)
            {
                var fromCat = catalog.Resolve(id);
                if (fromCat != null) return fromCat;
            }

#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<ItemDefinition>($"Assets/Inventory/Items/{id}.asset");
#else
            return null;
#endif
        }

        private static FishingSpot.FishSpawn Fish(ItemDefinition item, FishCatchTool tool, int count, bool moving, Color tint) =>
            new()
            {
                item = item,
                tool = tool,
                count = count,
                moving = moving,
                tint = tint
            };

        private static FishingSpot.LootSpawn Loot(ItemDefinition item, Color glow, Vector3 localOffset) =>
            new()
            {
                item = item,
                glowColor = glow,
                localOffset = localOffset
            };

        private static void CreateSpot(
            Transform parent,
            string name,
            WaterAnchor anchor,
            float radius,
            FishCatchTool tool,
            List<FishingSpot.FishSpawn> fish,
            List<FishingSpot.LootSpawn> loot = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = anchor.Position;
            var spot = go.AddComponent<FishingSpot>();
            spot.Configure(name, tool, radius, fish, loot, autoSpawnOnStart: true, water: anchor.Collider);
        }

        private static WaterAnchor ResolveWater(string waterName, Vector3 fallback, string preferParent)
        {
            Transform best = null;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (t == null || t.name != waterName) continue;
                if (!string.IsNullOrEmpty(preferParent) && !IsUnderNamedParent(t, preferParent))
                    continue;

                best = t;
                break;
            }

            if (best == null)
                return new WaterAnchor(fallback, null);

            var col = best.GetComponent<Collider>();
            var p = best.position;
            if (col != null)
                p = new Vector3(col.bounds.center.x, 0.15f, col.bounds.center.z);
            else
                p = new Vector3(p.x, 0.15f, p.z);

            return new WaterAnchor(p, col);
        }

        private static bool IsUnderNamedParent(Transform t, string parentName)
        {
            for (var p = t.parent; p != null; p = p.parent)
            {
                if (p.name == parentName)
                    return true;
            }

            return false;
        }
    }
}
