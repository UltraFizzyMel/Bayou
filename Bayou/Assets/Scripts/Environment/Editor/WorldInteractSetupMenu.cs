#if UNITY_EDITOR
using Bayou.Environment;
using UnityEditor;
using UnityEngine;

namespace Bayou.Environment.Editor
{
    public static class WorldInteractSetupMenu
    {
        [MenuItem("Bayou/Interact/Create Sign At Scene View")]
        public static void CreateSign()
        {
            var sign = WorldInteractBootstrap.CreateSign(ScenePivot());
            Undo.RegisterCreatedObjectUndo(sign, "Create Sign");
            Selection.activeGameObject = sign;
            MarkDirty();
        }

        [MenuItem("Bayou/Interact/Create Shiny Pickup At Scene View")]
        public static void CreatePickup()
        {
            var box = WorldInteractBootstrap.CreateBox(ScenePivot());
            Undo.RegisterCreatedObjectUndo(box, "Create Shiny Pickup");
            Selection.activeGameObject = box;
            MarkDirty();
        }

        private static Vector3 ScenePivot()
        {
            if (SceneView.lastActiveSceneView != null)
                return SceneView.lastActiveSceneView.pivot;
            return Vector3.zero;
        }

        private static void MarkDirty()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            if (scene.IsValid())
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        }
    }
}
#endif
