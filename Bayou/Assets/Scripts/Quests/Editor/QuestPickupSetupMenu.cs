#if UNITY_EDITOR
using Bayou.Inventory;
using Bayou.Quests;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Bayou.Quests.Editor
{
    public static class QuestPickupSetupMenu
    {
        private const string ShinyPath = "Assets/Inventory/Items/Item_ShinyPond.asset";

        [MenuItem("Bayou/Quests/Place Shiny In Church Pond (MovementTest)", false, 30)]
        public static void PlaceShinyInChurchPond()
        {
            var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(ShinyPath);
            if (item == null)
            {
                EditorUtility.DisplayDialog("Quest Pickup", $"Missing {ShinyPath}", "OK");
                return;
            }

            // Church pond water area in MovementTest (world space).
            var pos = new Vector3(-20.5f, 0.12f, -42f);

            var existing = Object.FindFirstObjectByType<PondShinyCollectible>();
            GameObject go;
            if (existing != null)
            {
                go = existing.gameObject;
                go.transform.position = pos;
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = "ShinyPondPickup";
                go.transform.position = pos;
                go.transform.localScale = Vector3.one * 0.35f;

                var col = go.GetComponent<SphereCollider>();
                col.isTrigger = true;
                col.radius = 1f;

                // Remove old E-pickup if present.
                var old = go.GetComponent<QuestItemPickup>();
                if (old != null)
                    Object.DestroyImmediate(old);

                var shiny = go.AddComponent<PondShinyCollectible>();
                var so = new SerializedObject(shiny);
                so.FindProperty("item").objectReferenceValue = item;
                so.ApplyModifiedPropertiesWithoutUndo();

                Undo.RegisterCreatedObjectUndo(go, "Place Shiny In Church Pond");
            }

            Selection.activeGameObject = go;
            EditorSceneManager.MarkSceneDirty(go.scene);
            Debug.Log("[Bayou] Shiny placed in church pond. Cast the rod net onto it (or scoop with hand net).");
        }
    }
}
#endif
