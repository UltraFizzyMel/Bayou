using UnityEngine;
using UnityEngine.Audio;

namespace Bayou.Audio
{
    /// <summary>
    /// Shared one-shot / loop helper. Assign an optional mixer group (SFX / Music).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SfxPlayer : MonoBehaviour
    {
        [SerializeField] private AudioSource source;
        [SerializeField] private AudioMixerGroup outputGroup;

        private void Awake() => EnsureSource();

        public void EnsureSource()
        {
            if (source == null)
                source = GetComponent<AudioSource>();
            if (source == null)
                source = gameObject.AddComponent<AudioSource>();

            source.playOnAwake = false;
            source.spatialBlend = 0f;
            if (outputGroup != null)
                source.outputAudioMixerGroup = outputGroup;
        }

        public void PlayOneShot(AudioClip clip, float volume = 1f)
        {
            if (clip == null) return;
            EnsureSource();
            source.PlayOneShot(clip, Mathf.Clamp01(volume));
        }

        public void PlayLoop(AudioClip clip, float volume = 1f)
        {
            if (clip == null) return;
            EnsureSource();
            if (source.clip == clip && source.isPlaying && source.loop)
                return;
            source.loop = true;
            source.clip = clip;
            source.volume = Mathf.Clamp01(volume);
            source.Play();
        }

        public void StopLoop()
        {
            if (source == null) return;
            source.loop = false;
            source.Stop();
            source.clip = null;
        }

        public bool IsPlayingClip(AudioClip clip) =>
            source != null && source.isPlaying && source.clip == clip;
    }
}
