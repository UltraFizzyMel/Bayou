using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

namespace Bayou.Audio
{
    /// <summary>
    /// Swaps area music without a main-thread decompress hitch.
    /// Clips load in the background. Once the new clip is ready, the old one
    /// crossfades out over about 1.5 seconds instead of cutting.
    /// The 3 second zone confirm lives in TransitionMusicDirector; this fade starts
    /// only when Play is called, which is after that confirm.
    /// </summary>
    public sealed class AreaMusicPlayback : MonoBehaviour
    {
        public static AreaMusicPlayback Instance { get; private set; }

        private const float CrossfadeSeconds = 1.5f;

        private struct Slot
        {
            public AudioSource Music;
            public AudioSource Ambient;
        }

        private Slot _a;
        private Slot _b;
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

        /// <summary>True while this clip is the one being loaded or already looping.</summary>
        public static bool IsCurrent(AudioClip music)
        {
            if (Instance == null || music == null || Instance._wantMusic != music)
                return false;
            if (Instance._swap != null)
                return true;
            return Playing(Instance._a.Music, music) || Playing(Instance._b.Music, music);
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
            _a = CreateSlot("A");
            _b = CreateSlot("B");
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private Slot CreateSlot(string label)
        {
            return new Slot
            {
                Music = CreateSource("Music" + label),
                Ambient = CreateSource("Ambient" + label)
            };
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
            if (music == _wantMusic && ambient == _wantAmbient && (_swap != null || IsSettled(music, ambient)))
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
            Silence(_a);
            Silence(_b);
        }

        private IEnumerator SwapWhenReady()
        {
            var music = _wantMusic;
            var ambient = _wantAmbient;
            yield return Prepare(music);
            yield return Prepare(ambient);
            if (music != _wantMusic || ambient != _wantAmbient)
                yield break;

            var incoming = Quieter();
            var outgoing = incoming.Music == _a.Music ? _b : _a;
            BeginClip(incoming.Music, music);
            BeginClip(incoming.Ambient, ambient);

            var musicOut = outgoing.Music != null ? outgoing.Music.volume : 0f;
            var ambientOut = outgoing.Ambient != null ? outgoing.Ambient.volume : 0f;
            var t = 0f;
            while (t < CrossfadeSeconds)
            {
                if (music != _wantMusic || ambient != _wantAmbient)
                    yield break;
                t += Time.unscaledDeltaTime;
                var k = Mathf.Clamp01(t / CrossfadeSeconds);
                SetVolume(incoming.Music, music, _musicVolume * k);
                SetVolume(incoming.Ambient, ambient, _ambientVolume * k);
                if (outgoing.Music != null)
                    outgoing.Music.volume = musicOut * (1f - k);
                if (outgoing.Ambient != null)
                    outgoing.Ambient.volume = ambientOut * (1f - k);
                yield return null;
            }

            if (music != _wantMusic || ambient != _wantAmbient)
                yield break;

            Silence(outgoing);
            SetVolume(incoming.Music, music, music != null ? _musicVolume : 0f);
            SetVolume(incoming.Ambient, ambient, ambient != null ? _ambientVolume : 0f);
            if (music == null)
                StopSource(incoming.Music);
            if (ambient == null)
                StopSource(incoming.Ambient);
            _swap = null;
        }

        private Slot Quieter()
        {
            var a = _a.Music != null && _a.Music.isPlaying ? _a.Music.volume : 0f;
            var b = _b.Music != null && _b.Music.isPlaying ? _b.Music.volume : 0f;
            return a <= b ? _a : _b;
        }

        private bool IsSettled(AudioClip music, AudioClip ambient)
        {
            var loud = Quieter().Music == _a.Music ? _b : _a;
            var quiet = loud.Music == _a.Music ? _b : _a;
            if (quiet.Music != null && quiet.Music.isPlaying && quiet.Music.volume > 0.02f)
                return false;
            return ClipSettled(loud.Music, music, _musicVolume) &&
                   ClipSettled(loud.Ambient, ambient, _ambientVolume);
        }

        private static bool ClipSettled(AudioSource src, AudioClip clip, float volume)
        {
            if (src == null) return clip == null;
            if (clip == null)
                return !src.isPlaying || src.volume <= 0.02f;
            return src.isPlaying && src.clip == clip && src.volume >= volume * 0.9f;
        }

        private void BeginClip(AudioSource src, AudioClip clip)
        {
            if (src == null) return;
            src.outputAudioMixerGroup = _group;
            if (clip == null)
            {
                src.volume = 0f;
                return;
            }

            if (src.clip == clip && src.isPlaying)
                return;

            src.clip = clip;
            src.volume = 0f;
            src.Play();
        }

        private static void SetVolume(AudioSource src, AudioClip clip, float volume)
        {
            if (src == null || clip == null) return;
            src.volume = volume;
        }

        private static void Silence(Slot slot)
        {
            StopSource(slot.Music);
            StopSource(slot.Ambient);
        }

        private static void StopSource(AudioSource src)
        {
            if (src == null) return;
            src.Stop();
            src.clip = null;
            src.volume = 0f;
        }

        private static bool Playing(AudioSource src, AudioClip clip)
        {
            return src != null && src.isPlaying && src.clip == clip;
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
