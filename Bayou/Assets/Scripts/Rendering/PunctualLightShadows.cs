using UnityEngine;
using UnityEngine.SceneManagement;

namespace Bayou.Rendering
{
    /// <summary>
    /// Lamps, tombs, and the carry lantern stay visible, but they do not cast realtime
    /// shadows. A bayou full of shadow-casting point lights overflows URP's additional-light
    /// shadow atlas. While the camera moves, that pass logs a stack trace every render
    /// and the editor drops frames. The directional sun still casts shadows.
    /// </summary>
    static class PunctualLightShadows
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            DisablePunctualShadows();
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => DisablePunctualShadows();

        static void DisablePunctualShadows()
        {
            var lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < lights.Length; i++)
            {
                var light = lights[i];
                if (light == null || light.type == LightType.Directional)
                    continue;
                if (light.shadows != LightShadows.None)
                    light.shadows = LightShadows.None;
            }
        }
    }
}
