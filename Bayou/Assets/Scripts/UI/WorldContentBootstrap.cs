using Bayou.Fishing;
using Bayou.World;
using UnityEngine.SceneManagement;

namespace Bayou.UI
{
    /// <summary>Play-mode hooks that are not baked into every scene yet.</summary>
    public static class WorldContentBootstrap
    {
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AfterSceneLoad()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Install(SceneManager.GetActiveScene().name);
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Install(scene.name);

        private static void Install(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName) || sceneName == "MainMenu") return;
            FishingZoneBootstrap.EnsureInScene();
            CalisteLocationDiscovery.Ensure();
            AccordionDisposal.Ensure();
            PauseMenu.Ensure();
            MinimapHud.Ensure();
        }
    }
}
