using System.Collections.Generic;
using Bayou.Inventory;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Bayou.Fishing.Editor
{
    /// <summary>
    /// Bakes the Foggy Marsh fishing dots at fixed world coordinates.
    /// These positions are the source of truth. Do not recluster them.
    /// </summary>
    public static class FoggyMarshFishingBake
    {
        private const string ScenePath = "Assets/Scenes/TerrainTest.unity";
        private const float SurfaceY = -2.09f;

        private readonly struct SpotDef
        {
            public readonly string Name;
            public readonly string AssetPath;
            public readonly FishCatchTool Tool;
            public readonly Vector3 Position;
            public readonly Color Tint;

            public SpotDef(string name, string assetPath, FishCatchTool tool, float x, float z, Color tint)
            {
                Name = name;
                AssetPath = assetPath;
                Tool = tool;
                Position = new Vector3(x, SurfaceY, z);
                Tint = tint;
            }
        }

        /// <summary>Batchmode entry. Places the fixed list and saves TerrainTest.</summary>
        public static void Bake()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            RemovePreviousFoggySpots();

            var root = GameObject.Find(FishingSpotBootstrap.RootName);
            if (root == null)
                root = new GameObject(FishingSpotBootstrap.RootName);

            var folder = new GameObject("FoggyMarshSpots");
            folder.transform.SetParent(root.transform, false);

            var placed = 0;
            foreach (var def in FixedSpots())
            {
                var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(def.AssetPath);
                if (item == null)
                {
                    Debug.LogError("[FoggyMarsh] Missing item " + def.AssetPath);
                    continue;
                }

                CreateSpot(folder.transform, def, item);
                placed++;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[FoggyMarsh] Placed {placed} fixed Foggy Marsh spots into {ScenePath}.");
        }

        private static IEnumerable<SpotDef> FixedSpots()
        {
            const string gar = "Assets/Resources/Bayou/Items/Item_SpottedGar.asset";
            const string catfish = "Assets/Inventory/Items/Item_ChannelCatfish.asset";
            const string flounder = "Assets/Resources/Bayou/Items/Item_Flounder.asset";
            const string molly = "Assets/Inventory/Items/Item_SailfinMolly.asset";
            const string eel = "Assets/Inventory/Items/Item_AmericanEel.asset";
            const string crawfish = "Assets/Resources/Bayou/Items/Item_Crawfish.asset";

            var garTint = new Color(0.25f, 0.45f, 1f, 1f);
            var catfishTint = new Color(0.85f, 0.18f, 0.16f, 1f);
            var flounderTint = new Color(0.95f, 0.82f, 0.15f, 1f);
            var mollyTint = new Color(0.45f, 0.78f, 0.95f, 1f);
            var eelTint = new Color(0.58f, 0.22f, 0.82f, 1f);
            var crawfishTint = new Color(0.2f, 0.75f, 0.28f, 1f);

            yield return new SpotDef("Fish_Foggy_Catfish_01", catfish, FishCatchTool.Rod, 95f, 142f, catfishTint);
            yield return new SpotDef("Fish_Foggy_Eel_01", eel, FishCatchTool.Rod, 95f, 144f, eelTint);
            yield return new SpotDef("Fish_Foggy_Crawfish_01", crawfish, FishCatchTool.Net, 63f, 54f, crawfishTint);
            yield return new SpotDef("Fish_Foggy_Molly_01", molly, FishCatchTool.Net, 85f, 52f, mollyTint);
            yield return new SpotDef("Fish_Foggy_Gar_01", gar, FishCatchTool.Rod, 73.4f, 96f, garTint);
            yield return new SpotDef("Fish_Foggy_Catfish_02", catfish, FishCatchTool.Rod, 107f, 94f, catfishTint);
            yield return new SpotDef("Fish_Foggy_Molly_02", molly, FishCatchTool.Net, 107f, 100f, mollyTint);
            yield return new SpotDef("Fish_Foggy_Gar_02", gar, FishCatchTool.Rod, 67.8f, 146.2f, garTint);
            yield return new SpotDef("Fish_Foggy_Flounder_01", flounder, FishCatchTool.Rod, 99f, 46f, flounderTint);
            yield return new SpotDef("Fish_Foggy_Catfish_03", catfish, FishCatchTool.Rod, 108.7f, 112.5f, catfishTint);
            yield return new SpotDef("Fish_Foggy_Crawfish_02", crawfish, FishCatchTool.Net, 157f, -8f, crawfishTint);
            yield return new SpotDef("Fish_Foggy_Eel_02", eel, FishCatchTool.Rod, 175f, -20f, eelTint);
            yield return new SpotDef("Fish_Foggy_Flounder_02", flounder, FishCatchTool.Rod, 177f, -16f, flounderTint);
            yield return new SpotDef("Fish_Foggy_Flounder_03", flounder, FishCatchTool.Rod, -7f, 24f, flounderTint);
            yield return new SpotDef("Fish_Foggy_Molly_03", molly, FishCatchTool.Net, -3f, 24f, mollyTint);
            yield return new SpotDef("Fish_Foggy_Eel_03", eel, FishCatchTool.Rod, 1f, 24f, eelTint);
            yield return new SpotDef("Fish_Foggy_Gar_03", gar, FishCatchTool.Rod, 137f, 204f, garTint);
            yield return new SpotDef("Fish_Foggy_Gar_04", gar, FishCatchTool.Rod, 137f, 208f, garTint);
            yield return new SpotDef("Fish_Foggy_Catfish_04", catfish, FishCatchTool.Rod, 137f, 210f, catfishTint);
            yield return new SpotDef("Fish_Foggy_Crawfish_03", crawfish, FishCatchTool.Net, 81f, 118f, crawfishTint);
            yield return new SpotDef("Fish_Foggy_Molly_04", molly, FishCatchTool.Net, 83f, 126f, mollyTint);
            yield return new SpotDef("Fish_Foggy_Crawfish_04", crawfish, FishCatchTool.Net, 93f, 110f, crawfishTint);
            yield return new SpotDef("Fish_Foggy_Eel_04", eel, FishCatchTool.Rod, 97f, 110f, eelTint);
            yield return new SpotDef("Fish_Foggy_Flounder_04", flounder, FishCatchTool.Rod, 99f, 110f, flounderTint);
            yield return new SpotDef("Fish_Foggy_Catfish_05", catfish, FishCatchTool.Rod, 74.7f, 106.3f, catfishTint);
            yield return new SpotDef("Fish_Foggy_Molly_05", molly, FishCatchTool.Net, 67.8f, 146.2f, mollyTint);
        }

        private static void CreateSpot(Transform parent, SpotDef def, ItemDefinition item)
        {
            var go = new GameObject(def.Name);
            go.transform.SetParent(parent, false);
            go.transform.position = def.Position;
            var spot = go.AddComponent<FishingSpot>();
            spot.Configure(
                def.Name,
                def.Tool,
                4.5f,
                new List<FishingSpot.FishSpawn>
                {
                    new()
                    {
                        item = item,
                        tool = def.Tool,
                        count = 1,
                        moving = true,
                        tint = def.Tint
                    }
                },
                loot: null,
                autoSpawnOnStart: true,
                water: null);

            var so = new SerializedObject(spot);
            so.FindProperty("snapToWaterOnPlay").boolValue = false;
            so.FindProperty("spawnY").floatValue = 0.12f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void RemovePreviousFoggySpots()
        {
            var spots = Object.FindObjectsByType<FishingSpot>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < spots.Length; i++)
            {
                if (spots[i] == null) continue;
                if (spots[i].gameObject.name.StartsWith("Fish_Foggy_"))
                    Object.DestroyImmediate(spots[i].gameObject);
            }

            var folder = GameObject.Find("FoggyMarshSpots");
            if (folder != null)
                Object.DestroyImmediate(folder);
        }
    }
}
