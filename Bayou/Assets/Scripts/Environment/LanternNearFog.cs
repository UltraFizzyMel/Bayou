using System.Collections.Generic;
using Bayou.Fishing;
using Bayou.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Bayou.Environment
{
    /// <summary>
    /// While the carry lantern is equipped, marsh fog particles near the player lose a little alpha.
    /// Distant banks stay thick. The lantern-radius upgrade widens that near band.
    /// </summary>
    public sealed class LanternNearFog : MonoBehaviour
    {
        private const float NearReach = 14f;
        private const float CloseAlpha = 0.62f;
        private const float EdgeAlpha = 0.78f;
        private const float ActiveScanSeconds = 0.4f;
        private const int RestoreBudgetPerTick = 32;

        private sealed class FogEntry
        {
            public ParticleSystem Ps;
            public int Id;
            public float X;
            public float Z;
            public ParticleSystem.MinMaxGradient Original;
            public ParticleSystem.MinMaxGradient Close;
            public ParticleSystem.MinMaxGradient Edge;
        }

        private readonly List<FogEntry> _entries = new();
        private readonly Dictionary<int, FogEntry> _byId = new();
        private readonly Dictionary<int, float> _lastMul = new();
        private readonly HashSet<int> _faded = new();
        private readonly List<int> _fadedScratch = new();
        private Bounds _marshBounds;
        private bool _boundsValid;
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
            var active = LanternIlluminating();
            if (!active)
            {
                if (_faded.Count > 0)
                    RestoreSlice();
                return;
            }

            if (Time.unscaledTime < _nextScan) return;
            _nextScan = Time.unscaledTime + ActiveScanSeconds;

            if (_entries.Count == 0)
                Collect();

            var player = PlayerLocator.Transform;
            if (player == null || !_boundsValid || !PlayerNearMarsh(player.position))
                return;

            Apply(player.position);
        }

        /// <summary>Marsh fog emitters only — never scan the whole scene for ParticleSystems.</summary>
        private void Collect()
        {
            _entries.Clear();
            _byId.Clear();
            _lastMul.Clear();
            _faded.Clear();
            _boundsValid = false;

            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid()) return;

            var roots = scene.GetRootGameObjects();
            var fogRoots = new List<Transform>(4);
            for (var i = 0; i < roots.Length; i++)
                GatherFogRoots(roots[i].transform, fogRoots);

            for (var r = 0; r < fogRoots.Count; r++)
            {
                var psList = fogRoots[r].GetComponentsInChildren<ParticleSystem>(true);
                for (var p = 0; p < psList.Length; p++)
                    TryAddEntry(psList[p]);
            }

            if (_entries.Count == 0) return;

            _marshBounds = _entries[0].Ps != null ? new Bounds(new Vector3(_entries[0].X, 0f, _entries[0].Z), Vector3.zero) : default;
            for (var i = 0; i < _entries.Count; i++)
            {
                var e = _entries[i];
                _marshBounds.Encapsulate(new Vector3(e.X, 0f, e.Z));
            }

            var pad = NearReach * 2.5f;
            if (WorldFlags.Has(WorldFlags.LanternRadiusUpgrade))
                pad *= 1.75f;
            _marshBounds.Expand(new Vector3(pad, 80f, pad));
            _boundsValid = true;
        }

        private void TryAddEntry(ParticleSystem ps)
        {
            if (ps == null) return;
            var id = ps.GetInstanceID();
            if (_byId.ContainsKey(id)) return;

            var pos = ps.transform.position;
            var main = ps.main;
            var original = main.startColor;
            var entry = new FogEntry
            {
                Ps = ps,
                Id = id,
                X = pos.x,
                Z = pos.z,
                Original = original,
                Close = ScaleAlpha(original, CloseAlpha),
                Edge = ScaleAlpha(original, EdgeAlpha)
            };
            _entries.Add(entry);
            _byId[id] = entry;
        }

        private static void GatherFogRoots(Transform node, List<Transform> outRoots)
        {
            if (node == null) return;
            if (IsMarshFogRoot(node.name))
                outRoots.Add(node);

            for (var i = 0; i < node.childCount; i++)
                GatherFogRoots(node.GetChild(i), outRoots);
        }

        private static bool IsMarshFogRoot(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            return name.IndexOf("Foggy Marsh fog", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("fog particle", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private bool PlayerNearMarsh(Vector3 playerPos)
        {
            var expanded = _marshBounds;
            expanded.Expand(4f);
            return expanded.Contains(new Vector3(playerPos.x, _marshBounds.center.y, playerPos.z));
        }

        private void Apply(Vector3 playerPos)
        {
            var reach = NearReach;
            if (WorldFlags.Has(WorldFlags.LanternRadiusUpgrade))
                reach *= 1.75f;

            var reachSq = reach * reach;
            var innerReachSq = (reach * 0.5f) * (reach * 0.5f);
            var px = playerPos.x;
            var pz = playerPos.z;

            for (var i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                if (entry.Ps == null) continue;

                var dx = entry.X - px;
                var dz = entry.Z - pz;
                var distSq = dx * dx + dz * dz;
                if (distSq > reachSq)
                {
                    RestoreEntry(entry);
                    continue;
                }

                var mul = distSq <= innerReachSq ? CloseAlpha : EdgeAlpha;
                FadeEntry(entry, mul);
            }
        }

        private void FadeEntry(FogEntry entry, float mul)
        {
            var id = entry.Id;
            if (_lastMul.TryGetValue(id, out var prev) && Mathf.Approximately(prev, mul))
                return;

            if (entry.Ps == null) return;
            var main = entry.Ps.main;
            main.startColor = Mathf.Approximately(mul, CloseAlpha) ? entry.Close : entry.Edge;
            _faded.Add(id);
            _lastMul[id] = mul;
        }

        private void RestoreEntry(FogEntry entry)
        {
            var id = entry.Id;
            if (!_faded.Contains(id))
                return;
            if (entry.Ps == null) return;
            //entry.Ps.main.startColor = entry.Original;
            _faded.Remove(id);
            _lastMul.Remove(id);
        }

        private void RestoreSlice()
        {
            if (_faded.Count == 0) return;

            _fadedScratch.Clear();
            foreach (var id in _faded)
                _fadedScratch.Add(id);

            var budget = RestoreBudgetPerTick;
            for (var i = 0; i < _fadedScratch.Count && budget > 0; i++)
            {
                var id = _fadedScratch[i];
                if (!_faded.Contains(id))
                    continue;
                if (!_byId.TryGetValue(id, out var entry))
                {
                    _faded.Remove(id);
                    _lastMul.Remove(id);
                    budget--;
                    continue;
                }

                if (entry.Ps != null)
                    //entry.Ps.main.startColor = entry.Original;
                _faded.Remove(id);
                _lastMul.Remove(id);
                budget--;
            }
        }

        /// <summary>True while a world lantern light is actually out (carry slot or held mesh).</summary>
        private static bool LanternIlluminating()
        {
            if (HeldLantern.IsAnyLit)
                return true;

            var eq = PlayerLocator.Equipment;
            return eq != null && eq.CurrentItem == BayouHeldItem.Lantern;
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