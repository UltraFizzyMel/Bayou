using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Volume sliders → AudioMixer (Master / SFX / Music) with PlayerPrefs persistence.
/// </summary>
public class AudioSettings : MonoBehaviour
{
    const string PrefMaster = "Bayou.Audio.MasterVolume";
    const string PrefSfx = "Bayou.Audio.SFXVolume";
    const string PrefMusic = "Bayou.Audio.MusicVolume";
    const float DefaultDb = 0f;

    [SerializeField] AudioMixer targetMixer;

    [SerializeField] TMP_Text masterVolumeLabel;
    [SerializeField] Slider masterVolume;

    [SerializeField] TMP_Text sfxVolumeLabel;
    [SerializeField] Slider sfxVolume;

    [SerializeField] TMP_Text musicVolumeLabel;
    [SerializeField] Slider musicVolume;

    public static bool IsOpen { get; private set; }
    public static AudioSettings Instance { get; private set; }

    private bool _suppressSave;

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
        if (IsOpen)
            SetOpen(false);
    }

    private void Start()
    {
        LoadAndApply(refreshSliders: true);
    }

    private void OnEnable()
    {
        // Canvas/panel may wake after prefs were applied elsewhere — keep UI in sync.
        if (targetMixer != null)
            LoadAndApply(refreshSliders: true);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void ApplySavedOnSceneLoad() => ApplySavedVolumesIfPresent();

    /// <summary>
    /// Apply saved volumes even when the settings UI GameObject is inactive.
    /// </summary>
    public static void ApplySavedVolumesIfPresent()
    {
        var all = Object.FindObjectsByType<AudioSettings>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var settings in all)
        {
            if (settings != null)
                settings.LoadAndApply(refreshSliders: false);
        }
    }

    public static void ToggleOpen()
    {
        var settings = ResolveAny();
        if (settings == null)
        {
            Debug.LogWarning("[AudioSettings] No AudioSettings in the scene.");
            return;
        }

        settings.SetOpen(!IsOpen);
    }

    public static void CloseIfOpen()
    {
        if (!IsOpen) return;
        var settings = ResolveAny();
        settings?.SetOpen(false);
    }

    public void SetOpen(bool open)
    {
        var root = GetPanelRoot();
        if (root != null)
        {
            // Scene had scale (0,0,0) on the canvas — that makes the UI invisible.
            var rt = root.transform as RectTransform;
            if (rt != null && rt.localScale == Vector3.zero)
                rt.localScale = Vector3.one;

            root.SetActive(open);
        }

        IsOpen = open && root != null && root.activeInHierarchy;

        if (IsOpen)
            LoadAndApply(refreshSliders: true);

        Bayou.GameplayPause.SyncFromUiState();
    }

    public void LoadAndApply(bool refreshSliders)
    {
        if (targetMixer == null) return;

        var master = PlayerPrefs.GetFloat(PrefMaster, ReadMixerOrDefault("MasterVolume"));
        var sfx = PlayerPrefs.GetFloat(PrefSfx, ReadMixerOrDefault("SFXVolume"));
        var music = PlayerPrefs.GetFloat(PrefMusic, ReadMixerOrDefault("MusicVolume"));

        ApplyDb("MasterVolume", master);
        ApplyDb("SFXVolume", sfx);
        ApplyDb("MusicVolume", music);

        if (!refreshSliders) return;

        _suppressSave = true;
        SetSlider(masterVolume, master, masterVolumeLabel, "Master Volume");
        SetSlider(sfxVolume, sfx, sfxVolumeLabel, "Sound Effect Volume");
        SetSlider(musicVolume, music, musicVolumeLabel, "Music Volume");
        _suppressSave = false;
    }

    public void OnMasterVolumeChanged(float newValue)
    {
        ApplyDb("MasterVolume", newValue);
        UpdateLabel(masterVolumeLabel, "Master Volume", newValue, masterVolume);
        if (!_suppressSave)
            Save(PrefMaster, newValue);
    }

    public void OnSFXVolumeChanged(float newValue)
    {
        ApplyDb("SFXVolume", newValue);
        UpdateLabel(sfxVolumeLabel, "Sound Effect Volume", newValue, sfxVolume);
        if (!_suppressSave)
            Save(PrefSfx, newValue);
    }

    public void OnMusicVolumeChanged(float newValue)
    {
        ApplyDb("MusicVolume", newValue);
        UpdateLabel(musicVolumeLabel, "Music Volume", newValue, musicVolume);
        if (!_suppressSave)
            Save(PrefMusic, newValue);
    }

    private void ApplyDb(string param, float db)
    {
        if (targetMixer != null)
            targetMixer.SetFloat(param, db);
    }

    private float ReadMixerOrDefault(string param)
    {
        if (targetMixer != null && targetMixer.GetFloat(param, out var value))
            return value;
        return DefaultDb;
    }

    private static void SetSlider(Slider slider, float value, TMP_Text label, string title)
    {
        if (slider != null)
            slider.SetValueWithoutNotify(Mathf.Clamp(value, slider.minValue, slider.maxValue));
        UpdateLabel(label, title, value, slider);
    }

    private static void UpdateLabel(TMP_Text label, string title, float db, Slider slider)
    {
        if (label == null) return;

        var percent = 100;
        if (slider != null && !Mathf.Approximately(slider.maxValue, slider.minValue))
            percent = Mathf.RoundToInt(Mathf.InverseLerp(slider.minValue, slider.maxValue, db) * 100f);

        label.text = $"{title} [{percent}%]";
    }

    private static void Save(string key, float value)
    {
        PlayerPrefs.SetFloat(key, value);
        PlayerPrefs.Save();
    }

    private GameObject GetPanelRoot()
    {
        var canvas = GetComponentInParent<Canvas>(true);
        return canvas != null ? canvas.gameObject : gameObject;
    }

    private static AudioSettings ResolveAny()
    {
        if (Instance != null) return Instance;
        return Object.FindFirstObjectByType<AudioSettings>(FindObjectsInactive.Include);
    }
}
