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
