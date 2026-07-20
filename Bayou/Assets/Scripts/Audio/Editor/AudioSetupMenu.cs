#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using Bayou.Audio;
using Bayou.Player;
using Bayou.Save;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

namespace Bayou.Audio.Editor
{
    public static class AudioSetupMenu
    {
        private const string PlayerPrefabPath = "Assets/Prefabs/Characters/Player.prefab";
        private const string CharacterSoundsFolder = "Assets/Audio/Character Sounds";
        private const string AreaMusicFolder = "Assets/Audio/Area Music";
        private const string MixerPath = "Assets/Audio/DefaultMixer.mixer";

        /// <summary>Public so playtest/MovementTest setup can call this without going through the menu.</summary>
        [MenuItem("Bayou/Audio/Wire Gameplay Audio (Player Prefab + Scene)", false, 40)]
        public static void WireGameplayAudio()
        {
            var clips = LoadClipsByName(CharacterSoundsFolder);
            var music = LoadClipsByName(AreaMusicFolder);
            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            var sfxGroup = FindMixerGroup(mixer, "SFX");
            var musicGroup = FindMixerGroup(mixer, "Music");

            WirePlayerPrefab(clips, sfxGroup);
            WireSceneBonfire(clips, sfxGroup);
            WireOrCreateAreaZones(music, musicGroup);

            AssetDatabase.SaveAssets();
            Debug.Log("[Bayou] Audio wired. Drag/replace clips on FishingAudio, PlayerLocomotionAudio, BonfireAudio, AreaMusicZone as needed.");
        }

        [MenuItem("Bayou/Audio/Create Empty Area Music Zone", false, 41)]
        public static void CreateEmptyAreaZone()
        {
            var go = new GameObject("AreaMusicZone");
            var col = go.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(40f, 20f, 40f);
            go.AddComponent<AreaMusicZone>();
            Selection.activeGameObject = go;
            Undo.RegisterCreatedObjectUndo(go, "Create Area Music Zone");
        }

        private static void WirePlayerPrefab(Dictionary<string, AudioClip> clips, AudioMixerGroup sfxGroup)
        {
            var root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                var sfx = root.GetComponent<SfxPlayer>() ?? root.AddComponent<SfxPlayer>();
                AssignSerialized(sfx, "outputGroup", sfxGroup);

                var fishing = root.GetComponent<FishingAudio>() ?? root.AddComponent<FishingAudio>();
                AssignSerialized(fishing, "sfx", sfx);
                AssignClip(fishing, "castConfirm", clips, "Cast Animation Confirm");
                AssignClip(fishing, "throwNet", clips, "Throwing Net");
                AssignClip(fishing, "rodCasting", clips, "Rod Casting");
                AssignClip(fishing, "handNetScoop", clips, "Throwing Net");
                AssignClip(fishing, "rodLanding", clips, "Rod Landing");
                AssignClip(fishing, "fishOnLine", clips, "Fish On Line");
                AssignClip(fishing, "reelingInFish", clips, "Reeling In Fish");
                AssignClip(fishing, "manSnaggingFish", clips, "Man Snagging Fish");

                var loco = root.GetComponent<PlayerLocomotionAudio>() ?? root.AddComponent<PlayerLocomotionAudio>();
                AssignSerialized(loco, "sfx", sfx);
                AssignSerialized(loco, "motor", root.GetComponent<BayouCharacterMotor>());
                AssignSerialized(loco, "waterSensor", root.GetComponent<BayouWaterSensor>());
                AssignClipArray(loco, "walkingInMarsh", Collect(clips, "Walking In Marsh 1", "Walking In Marsh 2"));
                AssignClipArray(loco, "wadingInWater", Collect(clips, "Wading In Water 1", "Wading In Water.2", "Wading In Water.3"));
                AssignClip(loco, "swimming", clips, "Swimming");

                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void WireSceneBonfire(Dictionary<string, AudioClip> clips, AudioMixerGroup sfxGroup)
        {
            var ui = Object.FindFirstObjectByType<BonfireUIController>();
            var interactable = Object.FindFirstObjectByType<BonfireInteractable>();
            var host = ui != null ? ui.gameObject : interactable != null ? interactable.gameObject : null;
            if (host == null)
            {
                Debug.LogWarning("[Bayou] No BonfireUIController/BonfireInteractable in the open scene — skip BonfireAudio.");
                return;
            }

            Undo.RegisterCompleteObjectUndo(host, "Wire Bonfire Audio");
            var sfx = host.GetComponent<SfxPlayer>() ?? Undo.AddComponent<SfxPlayer>(host);
            AssignSerialized(sfx, "outputGroup", sfxGroup);

            var bonfire = host.GetComponent<BonfireAudio>() ?? Undo.AddComponent<BonfireAudio>(host);
            AssignSerialized(bonfire, "sfx", sfx);
            AssignClip(bonfire, "strikingMatch", clips, "Striking Match");
            AssignClip(bonfire, "matchBurning", clips, "Match Burning");
            EditorUtility.SetDirty(host);
        }

        private static void WireOrCreateAreaZones(Dictionary<string, AudioClip> music, AudioMixerGroup musicGroup)
        {
            // Only auto-fill existing zones; don't spam the scene with new volumes.
            var zones = Object.FindObjectsByType<AreaMusicZone>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var zone in zones)
            {
                Undo.RegisterCompleteObjectUndo(zone, "Wire Area Music Zone");
                AssignSerialized(zone, "musicGroup", musicGroup);
                var name = zone.gameObject.name.ToLowerInvariant();
                if (name.Contains("church"))
                {
                    AssignClip(zone, "music", music, "Church Song 79bpm #10 merged");
                    AssignClip(zone, "ambient", music, "Church Area Ambient Noise");
                }
                else if (name.Contains("grave"))
                {
                    AssignClip(zone, "music", music, "Graveyard #02");
                    AssignClip(zone, "ambient", music, "Graveyard Area Ambient Noise");
                }
                else if (name.Contains("menu"))
                {
                    AssignClip(zone, "music", music, "Start Menu #01");
                }

                EditorUtility.SetDirty(zone);
            }

            var menu = Object.FindFirstObjectByType<MenuMusicPlayer>();
            if (menu != null)
            {
                AssignClip(menu, "startMenuMusic", music, "Start Menu #01");
                AssignSerialized(menu, "musicGroup", musicGroup);
                EditorUtility.SetDirty(menu);
            }
        }

        private static Dictionary<string, AudioClip> LoadClipsByName(string folder)
        {
            var map = new Dictionary<string, AudioClip>();
            if (!AssetDatabase.IsValidFolder(folder))
                return map;

            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { folder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip == null) continue;
                map[clip.name] = clip;
                map[Path.GetFileNameWithoutExtension(path)] = clip;
            }

            return map;
        }

        private static AudioClip[] Collect(Dictionary<string, AudioClip> clips, params string[] names)
        {
            var list = new List<AudioClip>();
            foreach (var n in names)
            {
                if (clips.TryGetValue(n, out var c) && c != null)
                    list.Add(c);
            }

            return list.ToArray();
        }

        private static void AssignClip(Object target, string field, Dictionary<string, AudioClip> clips, string clipName)
        {
            if (!clips.TryGetValue(clipName, out var clip) || clip == null)
            {
                Debug.LogWarning($"[Bayou] Missing AudioClip '{clipName}' for {target.GetType().Name}.{field}");
                return;
            }

            AssignSerialized(target, field, clip);
        }

        private static void AssignClipArray(Object target, string field, AudioClip[] value) =>
            AssignSerialized(target, field, value);

        private static void AssignSerialized(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null)
            {
                Debug.LogWarning($"[Bayou] Field '{field}' not found on {target.GetType().Name}");
                return;
            }

            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static void AssignSerialized(Object target, string field, Object[] values)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null || !prop.isArray)
            {
                Debug.LogWarning($"[Bayou] Array field '{field}' not found on {target.GetType().Name}");
                return;
            }

            prop.arraySize = values?.Length ?? 0;
            for (var i = 0; i < prop.arraySize; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static AudioMixerGroup FindMixerGroup(AudioMixer mixer, string nameContains)
        {
            if (mixer == null) return null;
            foreach (var g in mixer.FindMatchingGroups(string.Empty))
            {
                if (g != null && g.name.IndexOf(nameContains, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return g;
            }

            return null;
        }
    }
}
#endif
