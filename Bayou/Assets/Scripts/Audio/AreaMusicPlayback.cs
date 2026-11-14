using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

namespace Bayou.Audio
{
    /// <summary>
    /// Swaps area music without a main-thread decompress hitch.
    /// Clips load in the background; playback starts only once the data is ready.
    /// </summary>
    public sealed class AreaMusicPlayback : MonoBehaviour
    {
        public static AreaMusicPlayback Instance { get; private set; }

        private AudioSource _music;
        private AudioSource _ambient;
        private AudioClip _wantMusic;
        private AudioClip _wantAmbient;
        private float _musicVolume = 0.7f;
        private float _ambientVolume = 0.55f;
        private AudioMixerGroup _group;
        private Coroutine _swap;

        public static void Play(
            AudioClip music,
            AudioClip ambient,
            float musicVolume,
            float ambientVolume,
            AudioMixerGroup group)
        {
            if (music == null && ambient == null) return;
            var host = Ensure();
            host.Begin(music, ambient, musicVolume, ambientVolume, group);
        }

        public static void Stop(AudioClip music, AudioClip ambient)
        {
            if (Instance == null) return;
            Instance.StopIfCurrent(music, ambient);
        }

        private static AreaMusicPlayback Ensure()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("AreaMusicPlayback");
            DontDestroyOnLoad(go);
            return go.AddComponent<AreaMusicPlayback>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            _music = CreateSource("Music");
            _ambient = CreateSource("Ambient");
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private AudioSource CreateSource(string label)
        {
            var child = new GameObject(label);
            child.transform.SetParent(transform, false);
            var src = child.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = true;
            src.spatialBlend = 0f;
            src.volume = 0f;
            return src;
        }

        private void Begin(
            AudioClip music,
            AudioClip ambient,
            float musicVolume,
            float ambientVolume,
            AudioMixerGroup group)
        {
            if (music == _wantMusic && ambient == _wantAmbient && _music.isPlaying)
                return;

            _wantMusic = music;
            _wantAmbient = ambient;
            _musicVolume = musicVolume;
            _ambientVolume = ambientVolume;
            _group = group;
            if (_swap != null)
                StopCoroutine(_swap);
            _swap = StartCoroutine(SwapWhenReady());
        }

        private void StopIfCurrent(AudioClip music, AudioClip ambient)
        {
            if (music != _wantMusic && ambient != _wantAmbient) return;
            if (_swap != null)
                StopCoroutine(_swap);
            _swap = null;
            _wantMusic = null;
            _wantAmbient = null;
            if (_music != null) _music.Stop();
            if (_ambient != null) _ambient.Stop();
        }

        private IEnumerator SwapWhenReady()
        {
            var music = _wantMusic;
            var ambient = _wantAmbient;
            yield return Prepare(music);
            yield return Prepare(ambient);
            if (music != _wantMusic || ambient != _wantAmbient)
                yield break;

            Apply(_music, music, _musicVolume);
            Apply(_ambient, ambient, _ambientVolume);

            var t = 0f;
            const float fade = 0.35f;
            while (t < fade)
            {
                if (music != _wantMusic || ambient != _wantAmbient)
                    yield break;
                t += Time.unscaledDeltaTime;
                var k = Mathf.Clamp01(t / fade);
                if (_music != null && music != null)
                    _music.volume = _musicVolume * k;
                if (_ambient != null && ambient != null)
                    _ambient.volume = _ambientVolume * k;
                yield return null;
            }

            if (_music != null && music != null)
                _music.volume = _musicVolume;
            if (_ambient != null && ambient != null)
                _ambient.volume = _ambientVolume;
        }

        private void Apply(AudioSource src, AudioClip clip, float volume)
        {
            if (src == null) return;
            if (_group != null)
                src.outputAudioMixerGroup = _group;
            if (clip == null)
            {
                src.Stop();
                src.clip = null;
                return;
            }

            if (src.clip == clip && src.isPlaying)
                return;

            src.clip = clip;
            src.volume = 0f;
            src.Play();
        }

        private static IEnumerator Prepare(AudioClip clip)
        {
            if (clip == null) yield break;
            if (clip.loadState == AudioDataLoadState.Loaded) yield break;
            // LoadAudioData blocks the frame when the clip is not imported to load in the background.
            if (clip.loadState == AudioDataLoadState.Unloaded && clip.loadInBackground)
                clip.LoadAudioData();

            var frames = 0;
            while (clip.loadState == AudioDataLoadState.Loading && frames < 600)
            {
                frames++;
                yield return null;
            }
        }
    }
}
