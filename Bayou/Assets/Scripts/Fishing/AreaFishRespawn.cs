using System.Collections.Generic;
using Bayou.Fish;
using Bayou.World;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Bayou.Fishing
{
    /// <summary>
    /// Entering an area transition brings back fish that were caught in that area.
    /// Living fish are left where they are. Foggy marsh spot coordinates are not moved.
    /// </summary>
    public sealed class AreaFishRespawn : MonoBehaviour
    {
        private readonly HashSet<int> _inside = new();
        private Collider[] _zones = System.Array.Empty<Collider>();
        private float _nextScan;

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
            if (Object.FindFirstObjectByType<AreaFishRespawn>() != null) return;
            var go = new GameObject(nameof(AreaFishRespawn));
            go.AddComponent<AreaFishRespawn>();
        }

        private void Update()
        {
            if (Time.unscaledTime >= _nextScan)
            {
                _nextScan = Time.unscaledTime + 1.5f;
                CacheZones();
            }

            var player = Bayou.Player.PlayerLocator.Transform;
            if (player == null) return;
            var pos = player.position;

            for (var i = 0; i < _zones.Length; i++)
            {
                var zone = _zones[i];
                if (zone == null) continue;
                var id = zone.GetInstanceID();
                var inside = zone.bounds.Contains(pos);
                if (inside)
                {
                    if (_inside.Add(id))
                        RespawnFor(zone);
                }
                else
                {
                    _inside.Remove(id);
                }
            }
        }

        private void CacheZones()
        {
            var found = new List<Collider>();
            var cols = Object.FindObjectsByType<Collider>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (var i = 0; i < cols.Length; i++)
            {
                var col = cols[i];
                if (col == null || !IsTransition(col.gameObject.name)) continue;
                found.Add(col);
            }

            _zones = found.ToArray();
        }

        private static bool IsTransition(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            if (name.IndexOf("Transition", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (name.StartsWith("Into ", System.StringComparison.OrdinalIgnoreCase))
                return true;
            return false;
        }

        private static void RespawnFor(Collider zone)
        {
            var name = zone.gameObject.name;
            var foggy = name.IndexOf("Foggy", System.StringComparison.OrdinalIgnoreCase) >= 0;
            var brackish = name.IndexOf("Brackish", System.StringComparison.OrdinalIgnoreCase) >= 0;
            var origin = zone.bounds.center;

            var fish = Object.FindObjectsByType<BayouFish>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var revived = 0;
            for (var i = 0; i < fish.Length; i++)
            {
                var one = fish[i];
                if (one == null || !one.IsCaught) continue;
                var home = one.SpawnPosition;
                var match = foggy
                    ? PlayerRegionFlags.IsFoggyPosition(home)
                    : brackish
                        ? PlayerRegionFlags.IsBrackishPosition(home)
                        : FlatNear(home, origin, 70f);
                if (!match) continue;
                one.Revive();
                revived++;
            }

            if (revived > 0)
                Debug.Log($"[Fish] Restocked {revived} caught fish on entering {name}.");
        }

        private static bool FlatNear(Vector3 a, Vector3 b, float radius)
        {
            a.y = 0f;
            b.y = 0f;
            return (a - b).sqrMagnitude <= radius * radius;
        }
    }
}
