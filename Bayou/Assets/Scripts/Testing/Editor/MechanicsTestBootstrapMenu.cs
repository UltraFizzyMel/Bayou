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
                Debug.Log("[Bayou] MechanicsTestBootstrap already in the scene.");
                return;
            }

            var go = new GameObject("MechanicsTestBootstrap");
            Undo.RegisterCreatedObjectUndo(go, "Add Mechanics Bootstrap");
            go.AddComponent<MechanicsTestBootstrap>();
            Selection.activeGameObject = go;
            if (go.scene.IsValid())
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(go.scene);
            Debug.Log("[Bayou] Added MechanicsTestBootstrap. Play to use the HUD (` to hide).");
        }

        [MenuItem("Bayou/Test/Remove Mechanics Bootstrap", false, 6)]
        public static void RemoveFromScene()
        {
            var existing = Object.FindFirstObjectByType<MechanicsTestBootstrap>();
            if (existing == null)
            {
                Debug.Log("[Bayou] No MechanicsTestBootstrap in the scene.");
                return;
            }

            Undo.DestroyObjectImmediate(existing.gameObject);
            Debug.Log("[Bayou] Removed MechanicsTestBootstrap.");
        }
    }
}
#endif
