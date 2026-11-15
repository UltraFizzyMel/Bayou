using System;
using System.Collections.Generic;
using Bayou.Creatures;
using Bayou.Environment;
using Bayou.Fishing;
using Bayou.Player;
using Bayou.UI;
using Bayou.World;
using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Bayou.Testing
{
    /// <summary>
    /// Playtest bootstrap. In Play mode, teleports the player through fishing spots,
    /// fast-travel areas, and story points found in the loaded scene.
    /// Does not spawn fish, enemies, signs, or boxes.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FishingPlaytestBootstrap : MonoBehaviour
    {
        // Playtest bootstrap. On in the editor so a Play session can walk every hole.
        // Off in a player build unless this component is saved into a scene with the box checked.
#if UNITY_EDITOR
        [SerializeField] private bool enablePlaytestBootstrap = true;
#else
        [SerializeField] private bool enablePlaytestBootstrap;
#endif

        private readonly List<Transform> _spots = new();
        private readonly List<Transform> _areas = new();
        private readonly List<Transform> _story = new();
        private int _spotIndex = -1;
        private int _areaIndex = -1;
        private int _storyIndex = -1;
        private string _status = string.Empty;
        private GUIStyle _label;

        private static readonly string[] AreaNames =
        {
            "Foggy Marsh Area",
            "Brackish Shore Area",
            "Brackish Shore Pier 2",
            "Church Area",
            "Graveyard Area"
        };

        private static readonly string[][] StoryNames =
        {
            new[] { "Altar Foggy Marsh" },
            new[] { "Altar Graveyard" },
            new[] { "Altar Brackish Shore" },
            new[] { "Altar Sunken Grove" },
            new[] { "Big Bed Stone" },
            new[] { "Landry", "Father Landry" },
            new[] { "Caliste" },
            new[] { "Marie Claire Baptiste", "Marie-Claire", "Marie" },
            new[] { "Olivier Baptiste", "Olivier" },
            new[] { "Church Foggy Marsh Gate" },
            new[] { "Caliste Maze Gate", "Maze Gate" },
            new[] { "LanternPickup", "Lantern Pickup" },
            new[] { "Accordion", "Accordion Pickup" },
            new[] { "Magnolias" },
            new[] { "Foggy Marsh Dock" },
            new[] { "Piers for Brackish Shore" },
            new[] { "Brackish Shore Pier 2" }
        };

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
            if (string.Equals(scene.name, "MainMenu", StringComparison.OrdinalIgnoreCase))
                return;
            if (FindFirstObjectByType<FishingPlaytestBootstrap>() != null) return;

            var go = new GameObject(nameof(FishingPlaytestBootstrap));
            go.hideFlags = HideFlags.DontSave;
            go.AddComponent<FishingPlaytestBootstrap>();
        }

        private void OnEnable()
        {
#if ENABLE_INPUT_SYSTEM
            EnsureKeyboardDevice();
#endif
        }

        private void Update()
        {
            if (!enablePlaytestBootstrap) return;
            if (Blocked()) return;

#if ENABLE_INPUT_SYSTEM
            EnsureKeyboardDevice();
            if (Pressed(Key.F5)) CycleSpots(-1);
            else if (Pressed(Key.F6)) CycleSpots(1);
            else if (Pressed(Key.F7)) CycleAreas(1);
            else if (Pressed(Key.F8)) CycleStory(ShiftHeld() ? -1 : 1);
#endif
        }

        private void OnGUI()
        {
            if (!enablePlaytestBootstrap) return;
            var ev = Event.current;
            if (ev != null && ev.type != EventType.Repaint) return;

            _label ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                wordWrap = false,
                richText = false
            };

            var hint = "F5/F6 fish spots    F7 areas    F8 story points";
            var body = string.IsNullOrEmpty(_status) ? hint : _status + "\n" + hint;
            var rect = new Rect(12f, Screen.height - 56f, 980f, 48f);
            var prev = _label.normal.textColor;
            _label.normal.textColor = new Color(0f, 0f, 0f, 0.85f);
            GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), body, _label);
            _label.normal.textColor = Color.white;
            GUI.Label(rect, body, _label);
            _label.normal.textColor = prev;
        }

        private void CycleSpots(int dir)
        {
            RebuildSpots();
            if (_spots.Count == 0)
            {
                _status = "No fishing spots in this scene";
                return;
            }

            _spotIndex = Wrap(_spotIndex, dir, _spots.Count);
            var spot = _spots[_spotIndex];
            Teleport(spot.position);
            _status = Line(spot.name, _spotIndex, _spots.Count, ZoneLabel(spot));
        }

        private void CycleAreas(int dir)
        {
            RebuildAreas();
            if (_areas.Count == 0)
            {
                _status = "No fishing areas in this scene";
                return;
            }

            _areaIndex = Wrap(_areaIndex, dir, _areas.Count);
            var area = _areas[_areaIndex];
            Teleport(area.position);
            _status = Line(area.name, _areaIndex, _areas.Count, "area");
        }

        private void CycleStory(int dir)
        {
            RebuildStory();
            if (_story.Count == 0)
            {
                _status = "No story points in this scene";
                return;
            }

            _storyIndex = Wrap(_storyIndex, dir, _story.Count);
            var point = _story[_storyIndex];
            Teleport(point.position);
            _status = Line(point.name, _storyIndex, _story.Count, "story");
        }

        private static int Wrap(int index, int dir, int count)
        {
            if (count <= 0) return -1;
            if (index < 0)
                return dir < 0 ? count - 1 : 0;
            var next = index + dir;
            if (next < 0) return count - 1;
            if (next >= count) return 0;
            return next;
        }

        private static string Line(string name, int index, int count, string zone) =>
            $"{name}   {index + 1}/{count}   {zone}";

        private void RebuildSpots()
        {
            _spots.Clear();
            var seen = new HashSet<int>();

            var folder = GameObject.Find("FoggyMarshSpots");
            if (folder != null)
            {
                var root = folder.transform;
                for (var i = 0; i < root.childCount; i++)
                    AddSpot(root.GetChild(i), seen);
            }

            var spots = FindObjectsByType<FishingSpot>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (var i = 0; i < spots.Length; i++)
            {
                if (spots[i] != null)
                    AddSpot(spots[i].transform, seen);
            }

            _spots.Sort(CompareSpots);
            if (_spotIndex >= _spots.Count)
                _spotIndex = -1;
        }

        private void AddSpot(Transform t, HashSet<int> seen)
        {
            if (t == null || !seen.Add(t.GetInstanceID())) return;
            _spots.Add(t);
        }

        private static int CompareSpots(Transform a, Transform b)
        {
            var zone = string.Compare(ZoneLabel(a), ZoneLabel(b), StringComparison.Ordinal);
            if (zone != 0) return zone;
            return string.Compare(a.name, b.name, StringComparison.Ordinal);
        }

        private void RebuildAreas()
        {
            _areas.Clear();
            for (var i = 0; i < AreaNames.Length; i++)
            {
                var go = GameObject.Find(AreaNames[i]);
                if (go != null)
                    _areas.Add(go.transform);
            }

            var zones = FindObjectsByType<FishingZone>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (var i = 0; i < zones.Length; i++)
            {
                var zone = zones[i];
                if (zone == null) continue;
                if (NearAny(_areas, zone.transform.position, 15f)) continue;
                _areas.Add(zone.transform);
            }

            if (_areaIndex >= _areas.Count)
                _areaIndex = -1;
        }

        private void RebuildStory()
        {
            _story.Clear();
            var seen = new HashSet<int>();
            for (var i = 0; i < StoryNames.Length; i++)
            {
                var found = FindNamed(StoryNames[i]);
                if (found == null &&
                    StoryNames[i][0].IndexOf("Accordion", StringComparison.OrdinalIgnoreCase) >= 0)
                    found = FindByContains("accordion");
                if (found == null || !seen.Add(found.GetInstanceID())) continue;
                _story.Add(found);
            }

            if (_storyIndex >= _story.Count)
                _storyIndex = -1;
        }

        private static Transform FindNamed(string[] names)
        {
            for (var i = 0; i < names.Length; i++)
            {
                var go = GameObject.Find(names[i]);
                if (go != null) return go.transform;
            }

            return null;
        }

        private static Transform FindByContains(string token)
        {
            var all = FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (var i = 0; i < all.Length; i++)
            {
                var t = all[i];
                if (t != null && t.name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                    return t;
            }

            return null;
        }

        private static bool NearAny(List<Transform> points, Vector3 world, float radius)
        {
            var r2 = radius * radius;
            for (var i = 0; i < points.Count; i++)
            {
                if (points[i] == null) continue;
                var d = points[i].position - world;
                d.y = 0f;
                if (d.sqrMagnitude <= r2) return true;
            }

            return false;
        }

        private static string ZoneLabel(Transform t)
        {
            if (t == null) return "World";
            for (var p = t; p != null; p = p.parent)
            {
                if (p.name == "FoggyMarshSpots") return "Foggy Marsh";
            }

            var n = t.name;
            if (n.StartsWith("Fish_Foggy", StringComparison.Ordinal)) return "Foggy Marsh";
            if (n.StartsWith("Tutorial", StringComparison.Ordinal)) return "Tutorial";
            if (n.StartsWith("Church", StringComparison.Ordinal)) return "Church";
            if (n.StartsWith("Graveyard", StringComparison.Ordinal) || n.StartsWith("Caliste", StringComparison.Ordinal))
                return "Graveyard";
            if (n.IndexOf("Brackish", StringComparison.OrdinalIgnoreCase) >= 0)
                return BrackishLabel(t.position);

            PlayerRegionFlags.Classify(t.position, out var foggy, out var first);
            if (foggy) return "Foggy Marsh";
            if (first) return "Brackish Shore";
            if (NearNamed("Brackish Shore Pier 2", t.position, 60f)) return "Brackish Shore Pier 2";
            return "World";
        }

        private static string BrackishLabel(Vector3 world)
        {
            if (NearNamed("Brackish Shore Pier 2", world, 60f)) return "Brackish Shore Pier 2";
            return "Brackish Shore";
        }

        private static bool NearNamed(string objectName, Vector3 world, float radius)
        {
            var go = GameObject.Find(objectName);
            if (go == null) return false;
            var d = go.transform.position - world;
            d.y = 0f;
            return d.sqrMagnitude <= radius * radius;
        }

        private static void Teleport(Vector3 world)
        {
            var player = PlayerLocator.Transform;
            if (player == null)
            {
                var tagged = GameObject.FindGameObjectWithTag("Player");
                if (tagged != null)
                    player = tagged.transform;
            }

            if (player == null)
            {
                var motor = FindFirstObjectByType<BayouCharacterMotor>();
                if (motor != null)
                    player = motor.transform;
            }

            if (player == null)
            {
                Debug.LogWarning("[Playtest] Player not found (needs the Player tag).");
                return;
            }

            var dest = StandOnGround(player, world);
            var cc = player.GetComponent<CharacterController>();
            if (cc != null)
                cc.enabled = false;

            var rb = player.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.position = dest;
            }

            player.position = dest;

            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            if (cc != null)
                cc.enabled = true;
        }

        /// <summary>
        /// Same idea as FogBarrier.SnapDestOntoGround: keep the XZ, stand on
        /// CreatureController.TryGetWalkableHeight, and step out of solid geometry.
        /// </summary>
        private static Vector3 StandOnGround(Transform player, Vector3 dest)
        {
            var origin = dest;
            if (!TryPlace(player, ref dest))
                dest.y = origin.y + 1.5f;

            if (!StandsInsideGeometry(player, dest))
                return dest;

            for (var ring = 1; ring <= 4; ring++)
            {
                var dist = ring * 1.6f;
                for (var i = 0; i < 8; i++)
                {
                    var ang = i * Mathf.PI * 0.25f;
                    var tryPos = origin + new Vector3(Mathf.Cos(ang) * dist, 0f, Mathf.Sin(ang) * dist);
                    if (!TryPlace(player, ref tryPos)) continue;
                    if (!StandsInsideGeometry(player, tryPos))
                        return tryPos;
                }
            }

            dest = origin;
            dest.y += 1.5f;
            return dest;
        }

        private const float GroundClearance = 0.08f;
        private static readonly Collider[] OverlapHits = new Collider[16];

        private static bool TryPlace(Transform player, ref Vector3 dest)
        {
            if (!CreatureController.TryGetWalkableHeight(dest, player, out var ground))
                return false;

            var footDrop = 1f;
            var capsule = player.GetComponent<CapsuleCollider>();
            if (capsule != null)
                footDrop = Mathf.Max(0f, capsule.height * 0.5f - capsule.center.y);
            dest.y = ground + footDrop + GroundClearance;
            return true;
        }

        private static bool StandsInsideGeometry(Transform player, Vector3 dest)
        {
            var capsule = player.GetComponent<CapsuleCollider>();
            var center = dest + (capsule != null ? capsule.center : new Vector3(0f, 1f, 0f));
            var radius = capsule != null ? Mathf.Clamp(capsule.radius * 0.5f, 0.05f, 0.2f) : 0.15f;
            var count = Physics.OverlapSphereNonAlloc(
                center, radius, OverlapHits, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            var blocked = false;
            for (var i = 0; i < count; i++)
            {
                var col = OverlapHits[i];
                OverlapHits[i] = null;
                if (blocked || col == null) continue;
                if (col.transform == player || col.transform.IsChildOf(player)) continue;
                blocked = true;
            }

            return blocked;
        }

        private static bool Blocked() =>
            Bayou.GameplayPause.IsPaused ||
            Bayou.GameplayPause.BlocksWorldInteract ||
            Bayou.GameplayPause.IsDialoguePlaying ||
            AudioSettings.IsOpen ||
            QuestJournalHud.IsOpen ||
            PauseMenu.IsOpen ||
            SignReadUi.IsOpen;

#if ENABLE_INPUT_SYSTEM
        private static void EnsureKeyboardDevice()
        {
            if (Keyboard.current != null) return;
            foreach (var device in InputSystem.devices)
            {
                if (device is Keyboard)
                    return;
            }

            InputSystem.AddDevice<Keyboard>();
        }

        private static bool ShiftHeld()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            return kb != null && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed);
        }

        private static bool Pressed(Key key)
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null) return false;
            var control = kb[key];
            return control != null && control.wasPressedThisFrame;
        }
#endif
    }
}
