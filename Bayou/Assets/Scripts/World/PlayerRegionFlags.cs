using UnityEngine;
using UnityEngine.SceneManagement;

namespace Bayou.World
{
    /// <summary>
    /// Writes where the player is standing into Ink.
    /// Foggy Marsh Area (and foggy transition volumes) sets is_in_foggy_marsh.
    /// Brackish Shore Area / Piers for Brackish Shore sets is_at_first_brackishshore.
    /// Brackish Shore Pier 2 is the second shore: both flags stay false.
    /// </summary>
    public sealed class PlayerRegionFlags : MonoBehaviour
    {
        public const string FoggyVar = "is_in_foggy_marsh";
        public const string FirstBrackishVar = "is_at_first_brackishshore";

        private const float FoggyRadius = 150f;
        private const float FirstShoreRadius = 78f;
        private const float SecondShoreRadius = 55f;

        private float _nextPush;

        public static bool IsInFoggyMarsh { get; private set; }
        public static bool IsAtFirstBrackishShore { get; private set; }

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
            if (Object.FindFirstObjectByType<PlayerRegionFlags>() != null) return;
            var go = new GameObject(nameof(PlayerRegionFlags));
            go.AddComponent<PlayerRegionFlags>();
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextPush) return;
            _nextPush = Time.unscaledTime + 0.35f;
            PushToDialogue();
        }

        public static void PushToDialogue()
        {
            var player = Bayou.Player.PlayerLocator.Transform;
            if (player == null)
            {
                var motor = Object.FindFirstObjectByType<Bayou.Player.BayouCharacterMotor>();
                if (motor != null)
                    player = motor.transform;
            }

            var foggy = false;
            var first = false;
            if (player != null)
                Classify(player.position, out foggy, out first);

            IsInFoggyMarsh = foggy;
            IsAtFirstBrackishShore = first;

            var dialogue = DialogueManager.GetInstance();
            if (dialogue == null) return;
            dialogue.SetBoolVariable(FoggyVar, foggy);
            dialogue.SetBoolVariable(FirstBrackishVar, first);
        }

        public static void Classify(Vector3 world, out bool foggy, out bool firstShore)
        {
            foggy = false;
            firstShore = false;
            CacheAnchors();

            var second = FlatDistance(world, _pier2);
            var first = FlatDistance(world, _firstShore);
            var piers = FlatDistance(world, _piers);
            var fog = FlatDistance(world, _foggy);

            // Second shore wins over the shared parent area. Both Ink flags stay false there.
            if (second <= SecondShoreRadius && second < first && second < piers && second + 8f < fog)
                return;

            if (first <= FirstShoreRadius || piers <= 40f)
            {
                firstShore = true;
                return;
            }

            if (fog <= FoggyRadius || InsideFoggyVolume(world))
                foggy = true;
        }

        private static Vector3 _foggy = new(4.56f, 0f, 164.4f);
        private static Vector3 _firstShore = new(76.2f, 0f, -59.6f);
        private static Vector3 _piers = new(123.03f, 0f, -48.6f);
        private static Vector3 _pier2 = new(256.01f, 0f, 46.69f);
        private static Collider[] _fogVolumes = System.Array.Empty<Collider>();
        private static float _anchorCacheUntil;

        private static void CacheAnchors()
        {
            if (Time.unscaledTime < _anchorCacheUntil) return;
            _anchorCacheUntil = Time.unscaledTime + 2f;
            _foggy = FindPoint("Foggy Marsh Area", _foggy);
            _firstShore = FindPoint("Brackish Shore Area", _firstShore);
            _piers = FindPoint("Piers for Brackish Shore", _piers);
            _pier2 = FindPoint("Brackish Shore Pier 2", _pier2);

            var cols = Object.FindObjectsByType<Collider>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            var found = new System.Collections.Generic.List<Collider>();
            for (var i = 0; i < cols.Length; i++)
            {
                var col = cols[i];
                if (col == null) continue;
                var name = col.gameObject.name;
                if (name.IndexOf("Foggy Marsh", System.StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                if (name.IndexOf("Brackish", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;
                if (name.IndexOf("Barrier", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;
                found.Add(col);
            }

            _fogVolumes = found.ToArray();
        }

        public static bool IsFoggyPosition(Vector3 world)
        {
            Classify(world, out var foggy, out _);
            return foggy;
        }

        public static bool IsBrackishPosition(Vector3 world)
        {
            CacheAnchors();
            return FlatDistance(world, _firstShore) <= FirstShoreRadius + 25f ||
                   FlatDistance(world, _piers) <= 55f ||
                   FlatDistance(world, _pier2) <= SecondShoreRadius + 20f;
        }

        private static bool InsideFoggyVolume(Vector3 world)
        {
            for (var i = 0; i < _fogVolumes.Length; i++)
            {
                var col = _fogVolumes[i];
                if (col != null && col.bounds.Contains(world))
                    return true;
            }

            return false;
        }

        private static Vector3 FindPoint(string objectName, Vector3 fallback)
        {
            var anchor = GameObject.Find(objectName);
            return anchor != null ? anchor.transform.position : fallback;
        }

        private static float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }
    }
}
