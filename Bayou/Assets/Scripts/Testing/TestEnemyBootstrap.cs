using Bayou.Creatures;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Bayou.Testing
{
    /// <summary>Spawns placeholder test enemies if none were baked into the scene.</summary>
    public static class TestEnemyBootstrap
    {
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

            CreatureBootstrap.EnsureInScene();
        }
    }
}
