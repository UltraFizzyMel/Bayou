using System.Collections.Generic;
using Bayou.Fishing;
using Bayou.Inventory;
using Bayou.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Bayou.Environment
{
    /// <summary>
    /// After the player has the lantern, fog particles in the volume they are walking through
    /// lose a little alpha. Distant banks stay thick. The lantern-radius upgrade widens that near band.
    /// </summary>
    public sealed class LanternNearFog : MonoBehaviour
    {
        private const float NearReach = 14f;
        private const float CloseAlpha = 0.62f;
        private const float EdgeAlpha = 0.78f;

        private readonly List<ParticleSystem> _systems = new();
        private readonly Dictionary<int, ParticleSystem.MinMaxGradient> _original = new();
        private readonly HashSet<int> _faded = new();
        private float _nextScan;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Ensure();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Ensure();

        private static void Ensure()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid()) return;
            if (string.Equals(scene.name, "MainMenu", System.StringComparison.OrdinalIgnoreCase))
                return;
            if (Object.FindFirstObjectByType<LanternNearFog>() != null) return;
            var go = new GameObject(nameof(LanternNearFog));
            go.AddComponent<LanternNearFog>();
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextScan) return;
            _nextScan = Time.unscaledTime + 0.25f;
            if (_systems.Count == 0)
                Collect();
            Apply();
        }

        private void Collect()
        {
            _systems.Clear();
            var all = Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var cam = Camera.main;
            for (var i = 0; i < all.Length; i++)
            {
                var ps = all[i];
                if (ps == null || !IsLocalFog(ps.transform, cam)) continue;
                _systems.Add(ps);
            }
        }

        private void Apply()
        {
            var player = PlayerLocator.Transform;
            var hasLantern = PlayerObtainedLantern();
            var reach = NearReach;
            if (WorldFlags.Has(WorldFlags.LanternRadiusUpgrade))
                reach *= 1.75f;

            var playerPos = player != null ? player.position : Vector3.positiveInfinity;
            for (var i = 0; i < _systems.Count; i++)
            {
                var ps = _systems[i];
                if (ps == null) continue;
                var id = ps.GetInstanceID();
                if (!hasLantern || player == null)
                {
                    Restore(ps, id);
                    continue;
                }

                var delta = ps.transform.position - playerPos;
                delta.y = 0f;
                var dist = delta.magnitude;
                if (dist > reach)
                {
                    Restore(ps, id);
                    continue;
                }

                var mul = dist <= reach * 0.5f ? CloseAlpha : EdgeAlpha;
                Fade(ps, id, mul);
            }
        }

        private void Fade(ParticleSystem ps, int id, float mul)
        {
            var main = ps.main;
            if (!_original.ContainsKey(id))
                _original[id] = main.startColor;

            main.startColor = ScaleAlpha(_original[id], mul);
            _faded.Add(id);
        }

        private void Restore(ParticleSystem ps, int id)
        {
            if (!_faded.Contains(id) || !_original.TryGetValue(id, out var src))
                return;
            var main = ps.main;
            main.startColor = src;
            _faded.Remove(id);
        }

        private static bool PlayerObtainedLantern()
        {
            var inv = InventoryController.Instance;
            if (inv != null && inv.HasItemsById("Item_Lantern", 1))
                return true;

            if (HeldLantern.IsAnyLit)
                return true;

            var quests = QuestManager.Resolve();
            return quests != null &&
                   quests.TryGetQuest(QuestIds.CollectLantern, out var quest) &&
                   quest != null &&
                   quest.state == QuestState.FINISHED;
        }

        private static bool IsLocalFog(Transform t, Camera cam)
        {
            if (cam != null && t.IsChildOf(cam.transform) &&
                t.name.IndexOf("fog", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            var walk = t;
            while (walk != null)
            {
                var n = walk.name;
                if (n.IndexOf("Foggy Marsh fog", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    n.IndexOf("fog particle", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
                walk = walk.parent;
            }

            return false;
        }

        private static ParticleSystem.MinMaxGradient ScaleAlpha(ParticleSystem.MinMaxGradient src, float mul)
        {
            switch (src.mode)
            {
                case ParticleSystemGradientMode.TwoColors:
                {
                    var max = src.colorMax;
                    var min = src.colorMin;
                    max.a *= mul;
                    min.a *= mul;
                    return new ParticleSystem.MinMaxGradient(min, max);
                }
                case ParticleSystemGradientMode.Gradient:
                    return new ParticleSystem.MinMaxGradient(ScaleGradient(src.gradient, mul));
                case ParticleSystemGradientMode.TwoGradients:
                    return new ParticleSystem.MinMaxGradient(
                        ScaleGradient(src.gradientMin, mul),
                        ScaleGradient(src.gradientMax, mul));
                default:
                {
                    var c = src.color;
                    c.a *= mul;
                    return new ParticleSystem.MinMaxGradient(c);
                }
            }
        }

        private static Gradient ScaleGradient(Gradient source, float mul)
        {
            var copy = new Gradient();
            var colors = source.colorKeys;
            var alphas = source.alphaKeys;
            for (var i = 0; i < alphas.Length; i++)
            {
                var key = alphas[i];
                key.alpha *= mul;
                alphas[i] = key;
            }
            copy.SetKeys(colors, alphas);
            return copy;
        }
    }
}
