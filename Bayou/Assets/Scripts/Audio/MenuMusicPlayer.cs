using UnityEngine;

namespace Bayou.Audio
{
    /// <summary>
    /// Drag Start Menu music here. Enable on a menu scene object.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MenuMusicPlayer : MonoBehaviour
    {
        [SerializeField] private AudioClip startMenuMusic;
        [SerializeField] private AudioSource source;
        [SerializeField] private float volume = 0.75f;
        [SerializeField] private bool playOnEnable = true;

        private void Awake()
        {
            if (source == null)
                source = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.volume = volume;
        }

        private void OnEnable()
        {
            if (playOnEnable)
                Play();
        }

        public void Play()
        {
            if (startMenuMusic == null || source == null) return;
            source.clip = startMenuMusic;
            source.volume = volume;
            if (!source.isPlaying)
                source.Play();
        }

        public void Stop()
        {
            if (source != null)
                source.Stop();
        }
    }
}
