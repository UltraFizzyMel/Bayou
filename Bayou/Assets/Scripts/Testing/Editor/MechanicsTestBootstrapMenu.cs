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
                Undo.DestroyObjectImmediate(existing.gameObject);
                Debug.Log("[Bayou] Removed leftover MechanicsTestBootstrap.");
            }
            else
            {
                Debug.Log("[Bayou] Mechanics bootstrap is disabled.");
            }
        }

        private static void EditorSceneManagerMarkDirty(GameObject go)
        {
            if (go.scene.IsValid())
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(go.scene);
        }
    }
}
#endif
