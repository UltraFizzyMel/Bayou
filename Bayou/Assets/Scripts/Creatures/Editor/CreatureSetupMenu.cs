#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Bayou.Creatures.Editor
{
    public static class CreatureSetupMenu
    {
        private const string MovementTestPath = "Assets/Scenes/MovementTest.unity";

        [MenuItem("Bayou/Creatures/Bake Into MovementTest Scene")]
        public static void BakeIntoMovementTest()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Creatures", "Exit Play Mode before baking.", "OK");
                return;
            }

            var scene = EditorSceneManager.OpenScene(MovementTestPath, OpenSceneMode.Single);
            var root = CreatureBootstrap.CreateCreaturesInScene(replaceExisting: true);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = root;
            EditorGUIUtility.PingObject(root);
            Debug.Log($"[Bayou] Baked creatures into {MovementTestPath} ({root.transform.childCount} groups).");
        }

        /// <summary>Batchmode: Unity -batchmode -quit -executeMethod Bayou.Creatures.Editor.CreatureSetupMenu.BakeMovementTestBatch</summary>
        public static void BakeMovementTestBatch()
        {
            BakeIntoMovementTest();
        }

        [MenuItem("Bayou/Creatures/Create Prefabs (Snake + Crocodile)")]
        public static void CreatePrefabs()
        {
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Creatures", "Exit Play Mode first.", "OK");
                return;
            }

            EnsurePrefabFolder();

            var snake = CreatureBootstrap.CreateSnake(
                null,
                "Snake",
                Vector3.zero,
                new[]
                {
                    new Vector3(0f, 0f, 0f),
                    new Vector3(3f, 0f, 1.5f),
                    new Vector3(1.5f, 0f, 4f)
                });
            SavePrefab(snake, CreatureBootstrap.SnakePrefabPath);
            Object.DestroyImmediate(snake);

            var croc = CreatureBootstrap.CreateCrocodile(null, "Crocodile", Vector3.zero, 6f);
            SavePrefab(croc, CreatureBootstrap.CrocPrefabPath);
            Object.DestroyImmediate(croc);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var snakePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CreatureBootstrap.SnakePrefabPath);
            Selection.activeObject = snakePrefab;
            EditorGUIUtility.PingObject(snakePrefab);
            Debug.Log($"[Bayou] Prefabs ready:\n- {CreatureBootstrap.SnakePrefabPath}\n- {CreatureBootstrap.CrocPrefabPath}");
        }

        [MenuItem("Bayou/Creatures/Create Snake At Scene View")]
        public static void CreateSnakeAtView()
        {
            var pos = ScenePivot();
            var snake = CreatureBootstrap.CreateSnake(
                FindOrCreateRoot().transform,
                "Snake_New",
                pos,
                new[]
                {
                    pos,
                    pos + new Vector3(3f, 0f, 1.5f),
                    pos + new Vector3(1.5f, 0f, 4f),
                    pos + new Vector3(-1.5f, 0f, 2f)
                });
            Undo.RegisterCreatedObjectUndo(snake, "Create Snake");
            Selection.activeGameObject = snake;
            MarkDirty();
        }

        [MenuItem("Bayou/Creatures/Create Crocodile At Scene View")]
        public static void CreateCrocodileAtView()
        {
            var pos = ScenePivot();
            var croc = CreatureBootstrap.CreateCrocodile(
                FindOrCreateRoot().transform,
                "Crocodile_New",
                pos,
                radius: 6f);
            Undo.RegisterCreatedObjectUndo(croc, "Create Crocodile");
            Selection.activeGameObject = croc;
            MarkDirty();
        }

        [MenuItem("Bayou/Creatures/Add Tall Grass Volume to selection")]
        public static void AddTallGrass()
        {
            var go = Selection.activeGameObject;
            if (go == null)
            {
                go = new GameObject("TallGrassVolume");
                Undo.RegisterCreatedObjectUndo(go, "Create Tall Grass Volume");
            }

            var col = go.GetComponent<Collider>();
            if (col == null)
            {
                var box = Undo.AddComponent<BoxCollider>(go);
                box.isTrigger = true;
                box.size = new Vector3(4f, 2f, 4f);
            }
            else
            {
                col.isTrigger = true;
            }

            if (go.GetComponent<TallGrassVolume>() == null)
                Undo.AddComponent<TallGrassVolume>(go);

            Selection.activeGameObject = go;
            MarkDirty();
            Debug.Log("[Bayou] TallGrassVolume ready — player inside is hidden from creature vision.");
        }

        [MenuItem("Bayou/Creatures/Select Creatures Root")]
        private static void SelectRoot()
        {
            var root = GameObject.Find(CreatureBootstrap.RootName);
            if (root == null)
            {
                EditorUtility.DisplayDialog(
                    "Creatures",
                    "No Creatures root. Run Bayou/Creatures/Bake Into MovementTest Scene.",
                    "OK");
                return;
            }

            Selection.activeGameObject = root;
            EditorGUIUtility.PingObject(root);
        }

        private static GameObject FindOrCreateRoot()
        {
            var root = GameObject.Find(CreatureBootstrap.RootName);
            if (root != null) return root;
            root = new GameObject(CreatureBootstrap.RootName);
            Undo.RegisterCreatedObjectUndo(root, "Create Creatures Root");
            return root;
        }

        private static Vector3 ScenePivot()
        {
            if (SceneView.lastActiveSceneView != null)
                return SceneView.lastActiveSceneView.pivot;
            return Vector3.zero;
        }

        private static void EnsurePrefabFolder()
        {
            if (AssetDatabase.IsValidFolder(CreatureBootstrap.PrefabFolder))
                return;
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
                AssetDatabase.CreateFolder("Assets", "Prefabs");
            AssetDatabase.CreateFolder("Assets/Prefabs", "Creatures");
        }

        private static void SavePrefab(GameObject instance, string path)
        {
            PrefabUtility.SaveAsPrefabAsset(instance, path);
        }

        private static void MarkDirty()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.IsValid())
                EditorSceneManager.MarkSceneDirty(scene);
        }
    }
}
#endif
