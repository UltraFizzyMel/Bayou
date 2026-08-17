using Bayou.Inventory;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Bayou.Creatures
{
    /// <summary>
    /// Builds snake/croc enemies for playtests. Prefer baking via
    /// Bayou/Creatures/Bake Into MovementTest Scene.
    /// </summary>
    public static class CreatureBootstrap
    {
        public const string RootName = "Creatures";
        public const string PrefabFolder = "Assets/Prefabs/Creatures";
        public const string SnakePrefabPath = PrefabFolder + "/Snake.prefab";
        public const string CrocPrefabPath = PrefabFolder + "/Crocodile.prefab";

        /// <summary>Play-mode / build fallback when none were baked.</summary>
        public static void EnsureInScene()
        {
            if (Object.FindFirstObjectByType<CreatureController>() != null)
                return;
            if (GameObject.Find(RootName) != null)
                return;

            CreateCreaturesInScene(replaceExisting: false);
        }

        public static GameObject CreateCreaturesInScene(bool replaceExisting)
        {
            if (replaceExisting)
                RemoveExisting();

            if (!replaceExisting)
            {
                var existing = GameObject.Find(RootName);
                if (existing != null)
                    return existing;
                if (Object.FindFirstObjectByType<CreatureController>() != null)
                    return null;
            }

            var root = new GameObject(RootName);

            // Near Caliste pond — snake patrols the bank.
            CreateSnake(
                root.transform,
                "Snake_CalisteBank",
                new Vector3(-58f, 0.2f, 86f),
                new[]
                {
                    new Vector3(-58f, 0.2f, 86f),
                    new Vector3(-52f, 0.2f, 90f),
                    new Vector3(-55f, 0.2f, 95f),
                    new Vector3(-61f, 0.2f, 91f)
                });

            // Foggy marsh approach — croc wanders a patch.
            CreateCrocodile(
                root.transform,
                "Crocodile_FoggyMarsh",
                new Vector3(30f, 0.2f, 8f),
                radius: 7f);

            // Graveyard path — second snake for net practice.
            CreateSnake(
                root.transform,
                "Snake_GraveyardPath",
                new Vector3(-95f, 0.2f, 95f),
                new[]
                {
                    new Vector3(-95f, 0.2f, 95f),
                    new Vector3(-90f, 0.2f, 98f),
                    new Vector3(-92f, 0.2f, 102f)
                });

            return root;
        }

        public static GameObject CreateSnake(Transform parent, string name, Vector3 start, Vector3[] worldWaypoints)
        {
            var group = new GameObject(name);
            if (parent != null)
                group.transform.SetParent(parent, false);
            group.transform.position = start;

            // Waypoints are siblings of Body so they stay world-fixed while the snake moves.
            var wpRoot = new GameObject("Waypoints");
            wpRoot.transform.SetParent(group.transform, false);

            var waypoints = new Transform[worldWaypoints.Length];
            for (var i = 0; i < worldWaypoints.Length; i++)
            {
                var wp = new GameObject($"WP_{i + 1}");
                wp.transform.SetParent(wpRoot.transform, true);
                wp.transform.position = worldWaypoints[i];
                waypoints[i] = wp.transform;
            }

            var body = CreateBody(group.transform, "Body", start, new Color(0.35f, 0.75f, 0.3f),
                height: 1.2f, radius: 0.4f, scale: new Vector3(0.7f, 0.45f, 1.2f));
            body.AddComponent<CreatureSense>();
            var brain = body.AddComponent<CreatureController>();
            body.AddComponent<CreatureContactHazard>();

            var snakeItem = LoadSnakeItem();
            ApplySnakeDefaults(brain, waypoints, snakeItem);

            return group;
        }

        public static GameObject CreateCrocodile(Transform parent, string name, Vector3 center, float radius)
        {
            var group = new GameObject(name);
            if (parent != null)
                group.transform.SetParent(parent, false);
            group.transform.position = center;

            // Wander area stays world-fixed (sibling of Body).
            var areaGo = new GameObject("WanderArea");
            areaGo.transform.SetParent(group.transform, false);
            areaGo.transform.position = center;
            var area = areaGo.AddComponent<AreaBounds>();
            area.ConfigureCircle(radius);

            var body = CreateBody(group.transform, "Body", center, new Color(0.25f, 0.45f, 0.28f),
                height: 1.6f, radius: 0.55f, scale: new Vector3(1.1f, 0.4f, 1.8f));
            body.AddComponent<CreatureSense>();
            var brain = body.AddComponent<CreatureController>();
            body.AddComponent<CreatureContactHazard>();
            ApplyCrocDefaults(brain, area);

            return group;
        }

        private static GameObject CreateBody(
            Transform parent,
            string name,
            Vector3 worldPos,
            Color color,
            float height,
            float radius,
            Vector3 scale)
        {
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = name;
            body.transform.SetParent(parent, true);
            body.transform.position = worldPos;
            body.transform.localScale = scale;

            // Replace default collider with a trigger the net / contact hazard can use.
            var old = body.GetComponent<Collider>();
            if (old != null)
            {
                if (Application.isPlaying)
                    Object.Destroy(old);
                else
                    Object.DestroyImmediate(old);
            }

            var capsule = body.AddComponent<CapsuleCollider>();
            capsule.isTrigger = true;
            capsule.height = height;
            capsule.radius = radius;
            capsule.center = Vector3.zero;

            var visual = body.AddComponent<CreaturePlaceholderVisual>();
            visual.Configure(color);

            return body;
        }

        private static void ApplySnakeDefaults(CreatureController brain, Transform[] waypoints, ItemDefinition item)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                var so = new SerializedObject(brain);
                so.FindProperty("netBehavior").enumValueIndex = (int)CreatureNetBehavior.CatchOnNet;
                var arr = so.FindProperty("patrolWaypoints");
                arr.arraySize = waypoints.Length;
                for (var i = 0; i < waypoints.Length; i++)
                    arr.GetArrayElementAtIndex(i).objectReferenceValue = waypoints[i];
                if (item != null)
                    so.FindProperty("inventoryItemWhenCaught").objectReferenceValue = item;
                so.FindProperty("passiveSpeed").floatValue = 1.6f;
                so.FindProperty("activeSpeed").floatValue = 3.4f;
                so.ApplyModifiedPropertiesWithoutUndo();
                return;
            }
#endif
            brain.ConfigureAsSnake(waypoints, item);
        }

        private static void ApplyCrocDefaults(CreatureController brain, AreaBounds area)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                var so = new SerializedObject(brain);
                so.FindProperty("netBehavior").enumValueIndex = (int)CreatureNetBehavior.StunOnNet;
                so.FindProperty("wanderArea").objectReferenceValue = area;
                so.FindProperty("stunSeconds").floatValue = 2.5f;
                so.FindProperty("passiveSpeed").floatValue = 1.1f;
                so.FindProperty("activeSpeed").floatValue = 2.8f;
                so.ApplyModifiedPropertiesWithoutUndo();
                return;
            }
#endif
            brain.ConfigureAsCrocodile(area);
        }

        private static ItemDefinition LoadSnakeItem()
        {
#if UNITY_EDITOR
            var fromAssets = AssetDatabase.LoadAssetAtPath<ItemDefinition>(
                "Assets/Inventory/Items/Item_Snake.asset");
            if (fromAssets != null) return fromAssets;
#endif
            return Resources.Load<ItemDefinition>("Bayou/Items/Item_Snake");
        }

        private static void RemoveExisting()
        {
            var root = GameObject.Find(RootName);
            if (root != null)
                Object.DestroyImmediate(root);
        }
    }
}
