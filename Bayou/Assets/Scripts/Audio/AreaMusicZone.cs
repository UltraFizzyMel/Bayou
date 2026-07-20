using UnityEngine;
using UnityEngine.Audio;

namespace Bayou.Audio
{
    /// <summary>
    /// Trigger volume for area music/ambient. Drag clips, add a trigger collider.
    /// Plays on enter, and also if the player already stands inside when the zone loads.
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
        [SerializeField] private bool playIfPlayerAlreadyInside = true;

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

        private void Start()
        {
            if (!playIfPlayerAlreadyInside || !playOnEnter) return;
            if (IsPlayerInside())
                PlayZone();
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

        public void Configure(
            string name,
            AudioClip musicClip,
            AudioClip ambientClip,
            AudioMixerGroup group,
            bool enterPlay = true,
            bool exitStop = false)
        {
            zoneName = name;
            music = musicClip;
            ambient = ambientClip;
            musicGroup = group;
            playOnEnter = enterPlay;
            stopOnExit = exitStop;
            playIfPlayerAlreadyInside = true;

            if (musicSource != null)
                ConfigureSource(musicSource, musicVolume);
            if (ambientSource != null)
                ConfigureSource(ambientSource, ambientVolume);
        }

        private bool IsPlayerInside()
        {
            var col = GetComponent<Collider>();
            if (col == null) return false;

            var players = Object.FindObjectsByType<Bayou.Player.BayouCharacterMotor>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var motor in players)
            {
                if (motor == null) continue;
                var p = motor.transform.position;
                if (col.bounds.Contains(p))
                    return true;
            }

            return false;
        }

        private static bool IsPlayer(Collider other) =>
            other != null && (other.CompareTag("Player") || other.GetComponentInParent<Bayou.Player.BayouCharacterMotor>() != null);
    }
}
