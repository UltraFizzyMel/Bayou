#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Bayou.Quests.Editor
{
    public static class QuestMarkerSetupMenu
    {
        private const string MovementTestPath = "Assets/Scenes/MovementTest.unity";

        [MenuItem("Bayou/Quests/Bake Marker Targets Into MovementTest")]
        public static void BakeIntoMovementTest()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Quest Markers", "Exit Play Mode first.", "OK");
                return;
            }

            var scene = EditorSceneManager.OpenScene(MovementTestPath, OpenSceneMode.Single);
            var added = 0;

            added += EnsureTarget("ShinyPondPickup",
                new[] { "CollectPondItemQuest" },
                QuestMarkerTarget.MarkerRole.Objective,
                "Item_ShinyPond",
                "Pond shiny",
                new Vector3(0f, 0.9f, 0f));

            added += EnsureTarget("LanternPickup",
                new[] { "CollectLanternQuest" },
                QuestMarkerTarget.MarkerRole.Objective,
                "Item_Lantern",
                "Lantern",
                new Vector3(0f, 1.4f, 0f));

            // Turn-in NPCs — match common hierarchy names.
            added += EnsureTargetOnFirstMatch(
                new[] { "Caliste", "Caliste NPC", "NPC_Caliste" },
                new[] { "SnapperAndMollyQuest" },
                QuestMarkerTarget.MarkerRole.TurnIn,
                null,
                "Caliste",
                new Vector3(0f, 1.9f, 0f));

            added += EnsureTargetOnFirstMatch(
                new[] { "Zenon Landry", "Landry", "Father Landry", "Zenon", "NPC_Landry" },
                new[] { "CollectPondItemQuest", "CollectLanternQuest", "VisitGravesQuest" },
                QuestMarkerTarget.MarkerRole.TurnIn,
                null,
                "Father Landry",
                new Vector3(0f, 1.9f, 0f));

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Bayou] Quest marker targets wired ({added} objects). Marker HUD auto-spawns in play.");
        }

        [MenuItem("Bayou/Quests/Add Marker Target To Selection")]
        public static void AddToSelection()
        {
            var go = Selection.activeGameObject;
            if (go == null)
            {
                EditorUtility.DisplayDialog("Quest Markers", "Select a scene object first.", "OK");
                return;
            }

            if (go.GetComponent<QuestMarkerTarget>() == null)
                Undo.AddComponent<QuestMarkerTarget>(go);
            EditorUtility.SetDirty(go);
            Debug.Log($"[Bayou] QuestMarkerTarget on {go.name} — set quest ids / role in the Inspector.");
        }

        private static int EnsureTarget(
            string objectName,
            string[] questIds,
            QuestMarkerTarget.MarkerRole role,
            string itemId,
            string label,
            Vector3 offset)
        {
            var go = GameObject.Find(objectName);
            if (go == null)
            {
                Debug.LogWarning($"[Bayou] Quest marker: '{objectName}' not found in scene.");
                return 0;
            }

            ApplyTarget(go, questIds, role, itemId, label, offset);
            return 1;
        }

        private static int EnsureTargetOnFirstMatch(
            string[] names,
            string[] questIds,
            QuestMarkerTarget.MarkerRole role,
            string itemId,
            string label,
            Vector3 offset)
        {
            for (var i = 0; i < names.Length; i++)
            {
                var go = GameObject.Find(names[i]);
                if (go == null) continue;
                ApplyTarget(go, questIds, role, itemId, label, offset);
                return 1;
            }

            Debug.LogWarning($"[Bayou] Quest marker: no NPC match among [{string.Join(", ", names)}]. Add QuestMarkerTarget manually.");
            return 0;
        }

        private static void ApplyTarget(
            GameObject go,
            string[] questIds,
            QuestMarkerTarget.MarkerRole role,
            string itemId,
            string label,
            Vector3 offset)
        {
            var target = go.GetComponent<QuestMarkerTarget>();
            if (target == null)
                target = Undo.AddComponent<QuestMarkerTarget>(go);

            var so = new SerializedObject(target);
            so.FindProperty("role").enumValueIndex = (int)role;
            so.FindProperty("itemId").stringValue = itemId ?? "";
            so.FindProperty("labelOverride").stringValue = label ?? "";
            so.FindProperty("markerOffset").vector3Value = offset;
            var arr = so.FindProperty("questIds");
            arr.arraySize = questIds != null ? questIds.Length : 0;
            for (var i = 0; i < arr.arraySize; i++)
                arr.GetArrayElementAtIndex(i).stringValue = questIds[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(go);
        }
    }
}
#endif
