using UnityEditor;
using UnityEngine;

namespace Bayou.Fishing.Editor
{
    public static class FishingSpotSetupMenu
    {
        [MenuItem("Bayou/Fishing/Setup MovementTest Spots (Play Mode)")]
        private static void SetupSpots()
        {
            if (!Application.isPlaying)
            {
                EditorUtility.DisplayDialog(
                    "Fishing Spots",
                    "Enter Play Mode first — spots are created at runtime by FishingSpotBootstrap (also auto-runs from PlaytestHarness).",
                    "OK");
                return;
            }

            FishingSpotBootstrap.EnsureInScene();
            Debug.Log("[Bayou] FishingSpotBootstrap.EnsureInScene() called.");
        }

        [MenuItem("Bayou/Fishing/Select FishingSpots Root")]
        private static void SelectRoot()
        {
            var root = GameObject.Find("FishingSpots");
            if (root == null)
            {
                EditorUtility.DisplayDialog("Fishing Spots", "No FishingSpots root in the scene (enter Play Mode to create).", "OK");
                return;
            }

            Selection.activeGameObject = root;
            EditorGUIUtility.PingObject(root);
        }
    }
}
