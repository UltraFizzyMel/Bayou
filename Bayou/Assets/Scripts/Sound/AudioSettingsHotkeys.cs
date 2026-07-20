using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Always-active V / Esc listener so volume works when Audio Settings Canvas starts disabled
/// (player builds without PlaytestHarness).
/// </summary>
[DefaultExecutionOrder(-200)]
public sealed class AudioSettingsHotkeys : MonoBehaviour
{
    private static AudioSettingsHotkeys _instance;

    public static void EnsureInstalled()
    {
        if (_instance != null) return;
        var go = new GameObject(nameof(AudioSettingsHotkeys));
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<AudioSettingsHotkeys>();
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        if (WasTogglePressed())
            AudioSettings.ToggleOpen();

        if (AudioSettings.IsOpen && WasClosePressed())
            AudioSettings.CloseIfOpen();
    }

    private static bool WasTogglePressed()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        return kb != null && kb.vKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.V);
#endif
    }

    private static bool WasClosePressed()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        return kb != null && kb.escapeKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Escape);
#endif
    }
}
