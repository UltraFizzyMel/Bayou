using UnityEngine;
using UnityEngine.SceneManagement;

namespace Bayou.Environment
{
    /// <summary>Attaches fog push-back to Foggy Marsh transition volumes and a marsh interior.</summary>
    public static class FogBarrierBootstrap
    {
        public const string InteriorName = "FogBarrier_MarshInterior";

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

            var into = GameObject.Find("Into Foggy Marsh");
            Transform entrance = null;
            if (into != null)
            {
                for (var i = 0; i < into.transform.childCount; i++)
                {
                    var child = into.transform.GetChild(i);
                    if (child.GetComponent<Collider>() == null) continue;
                    if (child.GetComponent<FogBarrier>() == null)
                        child.gameObject.AddComponent<FogBarrier>();
                    var col = child.GetComponent<Collider>();
                    if (col != null)
                        col.isTrigger = true;
                    if (entrance == null || child.position.sqrMagnitude < entrance.position.sqrMagnitude)
                        entrance = child;
                }
            }

            FogBarrier first = null;
            var all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (var i = 0; i < all.Length; i++)
            {
                var t = all[i];
                if (t == null) continue;
                if (!IsFogTransition(t.name)) continue;

                var col = t.GetComponent<Collider>();
                if (col == null) continue;
                col.isTrigger = true;

                var barrier = t.GetComponent<FogBarrier>();
                if (barrier == null)
                    barrier = t.gameObject.AddComponent<FogBarrier>();
                if (first == null)
                    first = barrier;
            }

            if (entrance != null)
                EnsureInterior(entrance);
            else if (first != null)
                EnsureInterior(first.transform);
        }

        private static bool IsFogTransition(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            return name.IndexOf("Foggy Marsh", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("FogBarrier", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void EnsureInterior(Transform entrance)
        {
            if (GameObject.Find(InteriorName) != null)
                return;

            var go = new GameObject(InteriorName);
            go.transform.SetParent(entrance.parent != null ? entrance.parent : null, true);
            go.transform.position = entrance.position + entrance.forward * 18f;
            go.transform.rotation = entrance.rotation;

            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = new Vector3(0f, 4f, 0f);
            box.size = new Vector3(42f, 14f, 36f);
            go.AddComponent<FogBarrier>();
        }
    }
}
