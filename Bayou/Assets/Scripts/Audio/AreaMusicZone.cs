using UnityEngine;
using UnityEngine.Audio;

namespace Bayou.Audio
{
    /// <summary>
    /// Trigger volume for area music/ambient. Drag clips, add a trigger collider.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class AreaMusicZone : MonoBehaviour
    {
        [SerializeField] private string zoneName = "Area";
        [SerializeField] private AudioClip music;
        [SerializeField] private AudioClip ambient;
        [SerializeField] private float musicVolume = 0.7f;
        [SerializeField] private float ambientVolume = 0.55f;
        [SerializeField] private bool playOnEnter = true;
        [SerializeField] private bool stopOnExit;

        [Header("Mixer")]
        [SerializeField] private AudioMixerGroup musicGroup;

        [Header("Sources (auto-created if empty)")]
        [SerializeField] private AudioSource musicSource;
        [SerializeField] private AudioSource ambientSource;

        private void Reset()
        {
            var col = GetComponent<Collider>();
            if (col != null)
                col.isTrigger = true;
        }

        private void Awake()
        {
            musicSource = EnsureSource(musicSource, "MusicSource", musicVolume);
            ambientSource = EnsureSource(ambientSource, "AmbientSource", ambientVolume);
        }

        private AudioSource EnsureSource(AudioSource existing, string childName, float volume)
        {
            if (existing != null)
            {
                ConfigureSource(existing, volume);
                return existing;
            }

            var child = transform.Find(childName);
            var go = child != null ? child.gameObject : new GameObject(childName);
            if (child == null)
                go.transform.SetParent(transform, false);

            var src = go.GetComponent<AudioSource>() ?? go.AddComponent<AudioSource>();
            ConfigureSource(src, volume);
            return src;
        }

        private void ConfigureSource(AudioSource src, float volume)
        {
            src.playOnAwake = false;
            src.loop = true;
            src.spatialBlend = 0f;
            src.volume = volume;
            if (musicGroup != null)
                src.outputAudioMixerGroup = musicGroup;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!playOnEnter || !IsPlayer(other)) return;
            PlayZone();
        }

        private void OnTriggerExit(Collider other)
        {
            if (!stopOnExit || !IsPlayer(other)) return;
            StopZone();
        }

        public void PlayZone()
        {
            if (music != null)
            {
                musicSource.clip = music;
                musicSource.volume = musicVolume;
                if (musicGroup != null)
                    musicSource.outputAudioMixerGroup = musicGroup;
                if (!musicSource.isPlaying)
                    musicSource.Play();
            }

            if (ambient != null)
            {
                ambientSource.clip = ambient;
                ambientSource.volume = ambientVolume;
                if (musicGroup != null)
                    ambientSource.outputAudioMixerGroup = musicGroup;
                if (!ambientSource.isPlaying)
                    ambientSource.Play();
            }
        }

        public void StopZone()
        {
            if (musicSource != null)
                musicSource.Stop();
            if (ambientSource != null)
                ambientSource.Stop();
        }

        private static bool IsPlayer(Collider other) =>
            other != null && (other.CompareTag("Player") || other.GetComponentInParent<Bayou.Player.BayouCharacterMotor>() != null);
    }
}
