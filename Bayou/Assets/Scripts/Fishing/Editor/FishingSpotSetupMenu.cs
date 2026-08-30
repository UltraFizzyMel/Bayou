using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Bayou.Fishing.Editor
{
    public static class FishingSpotSetupMenu
    {
        private const string MovementTestPath = "Assets/Scenes/MovementTest.unity";

        [MenuItem("Bayou/Fishing/Bake Spots Into MovementTest Scene")]
        public static void BakeIntoMovementTest()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Fishing Spots", "Exit Play Mode before baking spots into the scene.", "OK");
                return;
            }

            var scene = EditorSceneManager.OpenScene(MovementTestPath, OpenSceneMode.Single);
            var root = FishingSpotBootstrap.CreateSpotsInScene(replaceExisting: true);
            if (root == null)
            {
                EditorUtility.DisplayDialog("Fishing Spots", "Bake failed — fish/rosary items missing from Assets/Inventory/Items.", "OK");
                return;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = root;
            Debug.Log($"[Bayou] Baked fishing spots into {MovementTestPath} ({root.transform.childCount} spots).");
        }

        /// <summary>Batchmode entry: Unity -batchmode -quit -executeMethod Bayou.Fishing.Editor.FishingSpotSetupMenu.BakeMovementTestBatch</summary>
        public static void BakeMovementTestBatch()
        {
            BakeIntoMovementTest();
        }

        [MenuItem("Bayou/Fishing/Snap Spots To Current Water")]
        public static void SnapSpotsToWater()
        {
            var spots = Object.FindObjectsByType<FishingSpot>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var n = 0;
            for (var i = 0; i < spots.Length; i++)
            {
                if (spots[i] == null || spots[i].WaterBounds == null) continue;
                Undo.RecordObject(spots[i].transform, "Snap fishing spot to water");
                spots[i].AlignToWater();
                EditorUtility.SetDirty(spots[i]);
                EditorUtility.SetDirty(spots[i].transform);
                n++;
            }

            if (!Application.isPlaying)
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"[Bayou] Snapped {n} fishing spots to their water meshes.");
        }

        [MenuItem("Bayou/Fishing/Select FishingSpots Root")]
        private static void SelectRoot()
        {
            var root = GameObject.Find(FishingSpotBootstrap.RootName);
            if (root == null)
            {
                EditorUtility.DisplayDialog(
                    "Fishing Spots",
                    "No FishingSpots root. Run Bayou/Fishing/Bake Spots Into MovementTest Scene.",
                    "OK");
                return;
            }

            Selection.activeGameObject = root;
            EditorGUIUtility.PingObject(root);
        }

        [MenuItem("Bayou/Fishing/Setup MovementTest Spots (Play Mode Fallback)")]
        private static void SetupSpotsPlayMode()
        {
            if (!Application.isPlaying)
            {
                EditorUtility.DisplayDialog(
                    "Fishing Spots",
                    "Prefer baking into the scene.\n\nFor a one-off Play Mode create, enter Play first — or use Bake Spots Into MovementTest Scene.",
                    "OK");
                return;
            }

            FishingSpotBootstrap.EnsureInScene();
            Debug.Log("[Bayou] FishingSpotBootstrap.EnsureInScene() called.");
        }
    }
}
