using UnityEngine;
using UnityEngine.SceneManagement;

namespace Bayou.Save
{
    /// <summary>
    /// Turns authored Fireplace / Campfire visuals into rest-and-save points.
    /// </summary>
    public static class CampfireBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            EnsureInActiveScene();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => EnsureInActiveScene();

        public static void EnsureInActiveScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid()) return;
            if (string.Equals(scene.name, "MainMenu", System.StringComparison.OrdinalIgnoreCase))
                return;

            var ui = Object.FindFirstObjectByType<BonfireUIController>(FindObjectsInactive.Include);
            var roots = Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            var index = 1;
            for (var i = 0; i < roots.Length; i++)
            {
                var t = roots[i];
                if (t == null || !IsCampfireRoot(t))
                    continue;
                Wire(t.gameObject, ui, index++);
            }

            EnsureTutorialPondCampfire(ui);
        }

        private static void EnsureTutorialPondCampfire(BonfireUIController ui)
        {
            if (!Application.isPlaying) return;

            var pond = new Vector3(16.6f, 0f, -57.8f);
            var spots = Object.FindObjectsByType<Bayou.Fishing.FishingSpot>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (var i = 0; i < spots.Length; i++)
            {
                var spot = spots[i];
                if (spot == null) continue;
                if (spot.name.IndexOf("Tutorial", System.StringComparison.OrdinalIgnoreCase) < 0 &&
                    (spot.SpotName == null || spot.SpotName.IndexOf("Tutorial", System.StringComparison.OrdinalIgnoreCase) < 0))
                    continue;
                pond = spot.transform.position;
                break;
            }

            var fires = Object.FindObjectsByType<BonfireInteractable>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (var i = 0; i < fires.Length; i++)
            {
                var fire = fires[i];
                if (fire == null) continue;
                var d = fire.transform.position - pond;
                d.y = 0f;
                if (d.sqrMagnitude <= 22f * 22f)
                    return;
            }

            var host = new GameObject("Campfire");
            host.transform.position = pond + new Vector3(6f, 0f, 6f);
            Bayou.Creatures.CreatureController.SnapBodyToGround(host.transform, 0.05f);

            var logs = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            logs.name = "Logs";
            logs.transform.SetParent(host.transform, false);
            logs.transform.localPosition = new Vector3(0f, 0.18f, 0f);
            logs.transform.localScale = new Vector3(0.7f, 0.12f, 0.7f);
            var logCol = logs.GetComponent<Collider>();
            if (logCol != null)
                logCol.enabled = false;

            var triggerGo = new GameObject("CampfireTrigger");
            triggerGo.transform.SetParent(host.transform, false);
            var sphere = triggerGo.AddComponent<SphereCollider>();
            sphere.isTrigger = true;
            sphere.radius = 2.4f;

            var interact = host.AddComponent<BonfireInteractable>();
            interact.Configure("campfire_tutorial_pond", "Campfire", ui);
        }

        private static bool IsCampfireRoot(Transform t)
        {
            if (t.GetComponent<BonfireInteractable>() != null)
                return false;

            var n = t.name;
            if (n.Equals("Fireplace", System.StringComparison.OrdinalIgnoreCase))
                return true;
            if (n.Equals("Campfire", System.StringComparison.OrdinalIgnoreCase) &&
                (t.parent == null || !t.parent.name.Equals("Fireplace", System.StringComparison.OrdinalIgnoreCase)))
                return true;
            return false;
        }

        private static void Wire(GameObject host, BonfireUIController ui, int index)
        {
            if (host.GetComponent<BonfireInteractable>() != null)
                return;

            var solid = host.GetComponent<SphereCollider>();
            if (solid != null)
                solid.enabled = false;

            Transform trigger = host.transform.Find("CampfireTrigger");
            if (trigger == null)
            {
                var go = new GameObject("CampfireTrigger");
                go.transform.SetParent(host.transform, false);
                go.transform.localPosition = Vector3.zero;
                var sphere = go.AddComponent<SphereCollider>();
                sphere.isTrigger = true;
                sphere.radius = 2.4f;
                trigger = go.transform;
            }
            else
            {
                var col = trigger.GetComponent<Collider>();
                if (col != null)
                    col.isTrigger = true;
            }

            var interact = host.AddComponent<BonfireInteractable>();
            var soName = $"campfire_{index:00}";
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                var so = new UnityEditor.SerializedObject(interact);
                so.FindProperty("bonfireId").stringValue = soName;
                so.FindProperty("displayName").stringValue = "Campfire";
                so.FindProperty("bonfireUi").objectReferenceValue = ui;
                so.FindProperty("restPrompt").stringValue = "Rest at the fire";
                so.ApplyModifiedPropertiesWithoutUndo();
                return;
            }
#endif
            ConfigureRuntime(interact, soName, ui);
        }

        private static void ConfigureRuntime(BonfireInteractable interact, string id, BonfireUIController ui)
        {
            // Serialized fields are private; Open() is driven by those values.
            // Use a small runtime helper on the interactable if present.
            interact.Configure(id, "Campfire", ui);
        }
    }
}
