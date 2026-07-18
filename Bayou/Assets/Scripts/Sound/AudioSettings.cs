using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;
using TMPro;

public class AudioSettings : MonoBehaviour
{
    [SerializeField] AudioMixer targetMixer;

    [SerializeField] TMP_Text masterVolumeLabel;
    [SerializeField] Slider masterVolume;

    [SerializeField] TMP_Text sfxVolumeLabel;
    [SerializeField] Slider sfxVolume;

    [SerializeField] TMP_Text musicVolumeLabel;
    [SerializeField] Slider musicVolume;

    const float audioVolumeOffset = 80f;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        float volumeValue;
        if (targetMixer.GetFloat("MasterVolume", out volumeValue))
        { masterVolume.value = volumeValue;  }
        if (targetMixer.GetFloat("SFXVolume", out volumeValue))
        { sfxVolume.value = volumeValue; }
        if (targetMixer.GetFloat("MusicVolume", out volumeValue))
        { musicVolume.value = volumeValue; }

    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void OnMasterVolumeChanged(float newValue)
    {
        masterVolumeLabel.text = "Master Volume [" + ((audioVolumeOffset + newValue).ToString() + "]");
        targetMixer.SetFloat("MasterVolume", newValue);
    }

    public void OnSFXVolumeChanged(float newValue)
    {
        sfxVolumeLabel.text = "Sound Effect Volume [" + ((audioVolumeOffset + newValue).ToString() + "]");
        targetMixer.SetFloat("SFXVolume", newValue);
    }

    public void OnMusicVolumeChanged(float newValue)
    {
        musicVolumeLabel.text = "Music Volume [" + ((audioVolumeOffset + newValue).ToString() + "]");
        targetMixer.SetFloat("MusicVolume", newValue);
    }
}
