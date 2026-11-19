using UnityEngine;
using UnityEngine.SceneManagement;

namespace Bayou.World
{
    /// <summary>
    /// Wires the three bayou altars and any bramble already in the scene.
    /// Adds the quest-lookup names the seal steps already ask for (Altar_FoggyMarsh, Altar_BrackishShore, Altar_Final).
    /// </summary>
    public static class AltarBrambleBootstrap
    {
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
            if (!scene.IsValid()) return;
            if (string.Equals(scene.name, "MainMenu", System.StringComparison.OrdinalIgnoreCase))
                return;

            Wire("Altar Foggy Marsh", BurnableInteract.FoggyFlag, "Altar_FoggyMarsh");
            Wire("Altar Graveyard", BurnableInteract.GraveFlag, null);
            Wire("Altar Brackish Shore", BurnableInteract.BrackishFlag, "Altar_BrackishShore");
            // Destroy the Witch's step looks for Altar_Final. The sunken grove altar is that place.
            EnsureAlias("Altar Sunken Grove", "Altar_Final");
            WireBramble();
        }

        private static void Wire(string objectName, string flag, string alias)
        {
            var altar = GameObject.Find(objectName);
            if (altar == null) return;
            if (!string.IsNullOrEmpty(alias))
                EnsureAlias(objectName, alias);

            var legacy = altar.GetComponent<BurnableInteract>();
            if (legacy != null)
            {
                legacy.enabled = false;
                Object.Destroy(legacy);
            }

            var burn = altar.GetComponent<AltarBurn>();
            if (burn == null)
                burn = altar.AddComponent<AltarBurn>();
            if (string.IsNullOrEmpty(burn.InkFlag))
                burn.Configure(flag);
        }

        private static void EnsureAlias(string hostName, string alias)
        {
            var host = GameObject.Find(hostName);
            if (host == null) return;
            if (host.transform.Find(alias) != null || GameObject.Find(alias) != null)
                return;

            var marker = new GameObject(alias);
            marker.transform.SetParent(host.transform, false);
            marker.transform.localPosition = Vector3.up * 0.5f;
        }

        private static void WireBramble()
        {
            var transforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < transforms.Length; i++)
            {
                var t = transforms[i];
                if (t == null || !ShouldWireBramble(t)) continue;

                // Legacy bootstrap used BurnableInteract (particles only, no mesh swap). Prefer BrambleBurn.
                var legacy = t.GetComponent<BurnableInteract>();
                if (legacy != null && string.IsNullOrEmpty(legacy.InkFlag))
                {
                    if (t.GetComponent<BrambleBurn>() == null)
                        t.gameObject.AddComponent<BrambleBurn>();
                    legacy.enabled = false;
                    Object.Destroy(legacy);
                    continue;
                }

                if (t.GetComponent<BrambleBurn>() != null) continue;
                t.gameObject.AddComponent<BrambleBurn>();
            }
        }

        private static bool ShouldWireBramble(Transform t)
        {
            if (t == null) return false;
            if (t.GetComponent<BrambleBurn>() != null) return true;
            var name = t.name;
            return IsBrambleName(name) && !IsBrambleAuxiliaryName(name) && HasBrambleVisuals(t);
        }

        private static bool HasBrambleVisuals(Transform t)
        {
            for (var i = 0; i < t.childCount; i++)
            {
                var child = t.GetChild(i);
                if (child == null) continue;
                var n = child.name;
                if (n.IndexOf("Normal", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
                if (n.IndexOf("Burning", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
                if (n.IndexOf("Burnt", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }

            return false;
        }

        private static bool IsBrambleAuxiliaryName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            if (name.IndexOf("group", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (name.IndexOf("location", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            // Particle-only children (Bramble Fire Top, etc.), not the plant root.
            if (name.IndexOf(" fire", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (name.StartsWith("Fire", System.StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static bool IsBrambleName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            return name.IndexOf("bramble", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("briar", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
