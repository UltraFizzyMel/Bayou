using UnityEngine;
using UnityEngine.SceneManagement;

namespace Bayou.Audio
{
    /// <summary>
    /// Area music follows the Transition Zones the level already has:
    /// Into Church, Into Graveyard, Into Foggy Marsh, Into Brackish Shore, Into Sunken Grove.
    /// Entering one starts a 3 second pending change. A different zone replaces it and
    /// resets the timer. When a pending zone stays pending for 3 seconds, playback
    /// crossfades to that area. Until the first confirm, the church track plays.
    /// </summary>
    public sealed class TransitionMusicDirector : MonoBehaviour
    {
        private const float ConfirmSeconds = 3f;
        private const float ChurchVolume = 0.7f;
        private const float GraveyardVolume = 0.7f;
        private const float SwampVolume = 0.62f;
        private const float AmbientVolume = 0.55f;

        public enum Area
        {
            Church,
            Graveyard,
            FoggyMarsh,
            BrackishShore,
            SunkenGrove
        }

        private AreaMusicSet _set;
        private GameplayMusicBootstrap _clips;
        private Area _confirmed = Area.Church;
        private Area _pending;
        private bool _hasPending;
        private float _pendingUntil;
        private bool _playing;
        private AudioClip _current;
        private bool _zonesBound;
        private int _bindAttempts;
        private float _nextVariant;
        private float _nextPlayRetry;
        private int _clipLookups;
        private bool _warnedSunken;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Ensure();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Ensure();

        private static void Ensure()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid()) return;
            if (string.Equals(scene.name, "MainMenu", System.StringComparison.OrdinalIgnoreCase))
                return;
            if (Object.FindFirstObjectByType<TransitionMusicDirector>() != null) return;
            var go = new GameObject(nameof(TransitionMusicDirector));
            go.AddComponent<TransitionMusicDirector>();
        }

        private void Awake()
        {
            _set = Resources.Load<AreaMusicSet>("Bayou/AreaMusicSet");
        }

        private void Start()
        {
            ResolveClips();
            WarmAll();
            _zonesBound = BindZones();
            PlayArea(Area.Church);
        }

        private void Update()
        {
            if (!_zonesBound && _bindAttempts < 8)
            {
                _bindAttempts++;
                _zonesBound = BindZones();
            }

            if (_hasPending && Time.unscaledTime >= _pendingUntil)
            {
                var area = _pending;
                _hasPending = false;
                if (area != _confirmed)
                {
                    _confirmed = area;
                    PlayArea(area);
                }
            }
            else if (!_hasPending && Time.unscaledTime >= _nextVariant)
            {
                _nextVariant = Time.unscaledTime + 0.5f;
                var clip = ClipFor(_confirmed);
                if (clip != null && clip != _current)
                    PlayArea(_confirmed);
            }
            else if (!_playing && Time.unscaledTime >= _nextPlayRetry)
            {
                // A missing clip used to call PlayArea, and therefore a scene-wide lookup, every frame.
                _nextPlayRetry = Time.unscaledTime + 0.5f;
                PlayArea(_confirmed);
            }
        }

        internal void NotifyEnter(Area area)
        {
            if (area == _confirmed && !_hasPending)
                return;
            if (_hasPending && _pending == area)
                return;

            _pending = area;
            _hasPending = true;
            _pendingUntil = Time.unscaledTime + ConfirmSeconds;
            Warm(ClipFor(area));
            Warm(AmbientFor(area));
        }

        private bool BindZones()
        {
            var parentGo = GameObject.Find("Transition Zones");
            if (parentGo == null)
                return false;

            var parent = parentGo.transform;
            var children = parent.GetComponentsInChildren<Transform>(true);
            for (var c = 0; c < children.Length; c++)
            {
                var child = children[c];
                if (child == null || child == parent) continue;
                if (!TryParse(child.name, out var area)) continue;
                if (child.GetComponent<TransitionMusicGate>() != null) continue;
                var armed = ArmTriggers(child.gameObject, area);
                if (child.GetComponent<TransitionMusicGate>() == null)
                {
                    var mark = child.gameObject.AddComponent<TransitionMusicGate>();
                    mark.MarkZoneRoot();
                }
                if (armed == 0 && area == Area.SunkenGrove && !_warnedSunken)
                {
                    _warnedSunken = true;
                    Debug.LogWarning(
                        "[TransitionMusic] Into Sunken Grove has no collider, so no trigger was added. " +
                        "Sunken grove music will not change until that object has a volume. " +
                        "Brackish Shore is the clip that would play.");
                }
                else if (armed == 0)
                {
                    Debug.LogWarning("[TransitionMusic] " + child.name + " has no collider to read.");
                }
            }

            return true;
        }

        private int ArmTriggers(GameObject zone, Area area)
        {
            var cols = zone.GetComponentsInChildren<Collider>(true);
            var triggers = 0;
            var solids = 0;
            for (var i = 0; i < cols.Length; i++)
            {
                var col = cols[i];
                if (col == null || UnderOtherZone(col.transform, zone.transform)) continue;
                if (col.isTrigger) triggers++;
                else solids++;
            }

            if (triggers == 0 && solids > 0)
            {
                for (var i = 0; i < cols.Length; i++)
                {
                    var col = cols[i];
                    if (col == null || col.isTrigger || UnderOtherZone(col.transform, zone.transform))
                        continue;
                    AddMatchingTrigger(col);
                }
            }

            cols = zone.GetComponentsInChildren<Collider>(true);
            var armed = 0;
            for (var i = 0; i < cols.Length; i++)
            {
                var col = cols[i];
                if (col == null || !col.isTrigger || UnderOtherZone(col.transform, zone.transform))
                    continue;
                var relay = col.GetComponent<TransitionMusicGate>();
                if (relay == null)
                    relay = col.gameObject.AddComponent<TransitionMusicGate>();
                relay.Arm(this, area);
                armed++;
            }

            return armed;
        }

        private static Collider AddMatchingTrigger(Collider source)
        {
            if (source is BoxCollider box)
            {
                var copy = source.gameObject.AddComponent<BoxCollider>();
                copy.isTrigger = true;
                copy.center = box.center;
                copy.size = box.size;
                return copy;
            }

            if (source is SphereCollider sphere)
            {
                var copy = source.gameObject.AddComponent<SphereCollider>();
                copy.isTrigger = true;
                copy.center = sphere.center;
                copy.radius = sphere.radius;
                return copy;
            }

            if (source is CapsuleCollider capsule)
            {
                var copy = source.gameObject.AddComponent<CapsuleCollider>();
                copy.isTrigger = true;
                copy.center = capsule.center;
                copy.radius = capsule.radius;
                copy.height = capsule.height;
                copy.direction = capsule.direction;
                return copy;
            }

            var added = source.gameObject.AddComponent<BoxCollider>();
            added.isTrigger = true;
            if (source is MeshCollider mesh && mesh.sharedMesh != null)
            {
                added.center = mesh.sharedMesh.bounds.center;
                added.size = mesh.sharedMesh.bounds.size;
                return added;
            }

            var world = source.bounds;
            added.center = source.transform.InverseTransformPoint(world.center);
            var size = source.transform.InverseTransformVector(world.size);
            added.size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
            return added;
        }

        private static bool UnderOtherZone(Transform start, Transform zone)
        {
            for (var t = start; t != null && t != zone; t = t.parent)
            {
                if (TryParse(t.name, out _))
                    return true;
            }

            return false;
        }

        private static bool TryParse(string objectName, out Area area)
        {
            area = Area.Church;
            if (string.IsNullOrEmpty(objectName)) return false;
            var name = objectName.Trim();
            if (name.Equals("Into Church", System.StringComparison.OrdinalIgnoreCase))
            {
                area = Area.Church;
                return true;
            }

            if (name.Equals("Into Graveyard", System.StringComparison.OrdinalIgnoreCase))
            {
                area = Area.Graveyard;
                return true;
            }

            if (name.Equals("Into Foggy Marsh", System.StringComparison.OrdinalIgnoreCase))
            {
                area = Area.FoggyMarsh;
                return true;
            }

            if (name.Equals("Into Brackish Shore", System.StringComparison.OrdinalIgnoreCase))
            {
                area = Area.BrackishShore;
                return true;
            }

            if (name.Equals("Into Sunken Grove", System.StringComparison.OrdinalIgnoreCase))
            {
                area = Area.SunkenGrove;
                return true;
            }

            return false;
        }

        private void ResolveClips()
        {
            if (_set == null)
                _set = Resources.Load<AreaMusicSet>("Bayou/AreaMusicSet");
            if (_clips == null && _clipLookups < 4)
            {
                _clipLookups++;
                var found = Object.FindObjectsByType<GameplayMusicBootstrap>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                if (found.Length > 0)
                    _clips = found[0];
            }
        }

        private void PlayArea(Area area)
        {
            ResolveClips();
            var clip = ClipFor(area);
            if (clip == null) return;
            var ambient = AmbientFor(area);
            _current = clip;
            _playing = true;
            var musicVolume = area == Area.FoggyMarsh || area == Area.BrackishShore || area == Area.SunkenGrove
                ? SwampVolume
                : area == Area.Graveyard ? GraveyardVolume : ChurchVolume;
            var group = area == Area.Church || area == Area.Graveyard
                ? _clips != null ? _clips.MusicGroup : null
                : null;
            AreaMusicPlayback.Play(clip, ambient, musicVolume, ambient != null ? AmbientVolume : 0f, group);
        }

        private AudioClip ClipFor(Area area)
        {
            ResolveClips();
            var accordion = SwampMusicDirector.WantsAccordion();
            switch (area)
            {
                case Area.Graveyard:
                    return _clips != null ? _clips.GraveyardMusic : null;
                case Area.FoggyMarsh:
                    return _set == null ? null : accordion ? _set.foggyWith : _set.foggyWithout;
                case Area.BrackishShore:
                case Area.SunkenGrove:
                    return _set == null ? null : accordion ? _set.brackishWith : _set.brackishWithout;
                default:
                    return _clips != null ? _clips.ChurchMusic : null;
            }
        }

        private AudioClip AmbientFor(Area area)
        {
            if (_clips == null) return null;
            if (area == Area.Church) return _clips.ChurchAmbient;
            if (area == Area.Graveyard) return _clips.GraveyardAmbient;
            return null;
        }

        private void WarmAll()
        {
            ResolveClips();
            Warm(_clips != null ? _clips.ChurchMusic : null);
            Warm(_clips != null ? _clips.ChurchAmbient : null);
            Warm(_clips != null ? _clips.GraveyardMusic : null);
            Warm(_clips != null ? _clips.GraveyardAmbient : null);
            if (_set == null) return;
            Warm(_set.foggyWithout);
            Warm(_set.foggyWith);
            Warm(_set.brackishWithout);
            Warm(_set.brackishWith);
        }

        private static void Warm(AudioClip clip)
        {
            if (clip == null) return;
            if (clip.loadState == AudioDataLoadState.Unloaded && clip.loadInBackground)
                clip.LoadAudioData();
        }

    }
}
