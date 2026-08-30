#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Bayou.Testing.Editor
{
    public static class MechanicsTestBootstrapMenu
    {
        [MenuItem("Bayou/Test/Add Mechanics Bootstrap", false, 5)]
        public static void AddToScene()
        {
            var existing = Object.FindFirstObjectByType<MechanicsTestBootstrap>();
            if (existing != null)
            {
                Selection.activeGameObject = existing.gameObject;
                EditorGUIUtility.PingObject(existing.gameObject);
                Debug.Log("[Bayou] MechanicsTestBootstrap is already in the scene. Press Play — HUD is top-left.");
                return;
            }

            var go = new GameObject("MechanicsTestBootstrap");
            Undo.RegisterCreatedObjectUndo(go, "Add Mechanics Bootstrap");
            go.AddComponent<MechanicsTestBootstrap>();
            Selection.activeGameObject = go;
            EditorSceneManagerMarkDirty(go);
            Debug.Log("[Bayou] Added MechanicsTestBootstrap. Press Play — HUD is top-left (` to hide).");
        }

        private static void EditorSceneManagerMarkDirty(GameObject go)
        {
            if (go.scene.IsValid())
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(go.scene);
        }
    }
}
#endif
