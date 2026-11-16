using Bayou.Quests;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Bayou.World
{
    /// <summary>Markers and flower placement for A Grave Revelation — Caliste's grave only.</summary>
    public static class VisitGravesQuestBootstrap
    {
        public const string CalisteGraveName = "Caliste's Grave";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Ensure();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Ensure();

        public static void Ensure()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || scene.name == "MainMenu") return;

            var grave = FindCalisteGraveFallback() ?? GameObject.Find(CalisteGraveName);
            if (grave == null) return;

            EnsureFlowerPlacement(grave);
            EnsureObjectiveMarker(grave.transform);
            RemoveMisplacedMarkers();
        }

        private static void RemoveMisplacedMarkers()
        {
            var markers = Object.FindObjectsByType<QuestMarkerTarget>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < markers.Length; i++)
            {
                var marker = markers[i];
                if (marker == null) continue;
                if (!marker.MatchesQuest(QuestIds.VisitGraves, turnIn: false)) continue;
                var label = marker.Label ?? "";
                if (label.IndexOf("Caliste", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;
                if (marker.transform != null &&
                    marker.transform.IsChildOf(GameObject.Find(CalisteGraveName)?.transform))
                    continue;
                Object.Destroy(marker);
            }
        }

        public static GameObject FindCalisteGraveFallback()
        {
            var transforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < transforms.Length; i++)
            {
                var t = transforms[i];
                if (t == null) continue;
                var n = t.name;
                if (n.IndexOf("Caliste", System.StringComparison.OrdinalIgnoreCase) >= 0 &&
                    n.IndexOf("Grave", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return t.gameObject;
            }

            return null;
        }

        private static void EnsureFlowerPlacement(GameObject grave)
        {
            if (grave.GetComponent<GraveFlowerPlacement>() == null)
                grave.AddComponent<GraveFlowerPlacement>();

            var altar = GameObject.Find("Altar Graveyard");
            if (altar != null && altar != grave)
            {
                var misplaced = altar.GetComponent<GraveFlowerPlacement>();
                if (misplaced != null)
                    Object.Destroy(misplaced);
            }
        }

        private static void EnsureObjectiveMarker(Transform grave)
        {
            var marker = grave.GetComponent<QuestMarkerTarget>();
            if (marker == null)
                marker = grave.gameObject.AddComponent<QuestMarkerTarget>();
            marker.Bind(QuestIds.VisitGraves, null, "Caliste's grave", QuestMarkerTarget.MarkerRole.Objective);
        }
    }
}