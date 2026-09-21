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

        private static readonly Color SnakeColor = new(0.42f, 0.86f, 0.28f, 1f);
        private static readonly Color CrocColor = new(0.22f, 0.42f, 0.22f, 1f);

        /// <summary>Play-mode / build fallback. Adds missing quest-route creatures and repairs visuals.</summary>
        public static void EnsureInScene()
        {
            CreaturePlaceholderVisual.PatchAll();
            CreateCreaturesInScene(replaceExisting: false);
            AdoptLooseAnimatedSnakes();
        }

        public static GameObject CreateCreaturesInScene(bool replaceExisting)
        {
            if (replaceExisting)
                RemoveExisting();

            var root = GameObject.Find(RootName);
            if (root == null)
                root = new GameObject(RootName);

            var lantern = Anchor("LanternPickup", new Vector3(5.37f, 0.6f, 119.3f));
            var caliste = Anchor("CalistePond_Net", new Vector3(73.62f, 0.2f, 16.36f));
            var graveyard = Anchor("Graveyard_Entrance_Net", new Vector3(66.31f, 0.2f, -26.35f));
            var marshGate = Anchor("Foggy Marsh Transition", new Vector3(32.6f, 0.2f, 2.19f));
            var lanternPath = Vector3.Lerp(marshGate, lantern, 0.55f);

            // Near player spawn — easy to test chase / net melee.
            EnsureSnake(
                root.transform,
                "Snake_TestNearSpawn",
                new Vector3(-4f, 1.6f, -82f),
                new[]
                {
                    new Vector3(-4f, 1.6f, -82f),
                    new Vector3(2f, 1.6f, -78f),
                    new Vector3(-1f, 1.6f, -74f),
                    new Vector3(-8f, 1.6f, -78f)
                });

            EnsureSnake(
                root.transform,
                "Snake_CalisteBank",
                caliste + new Vector3(-4.5f, 0.2f, 3f),
                Ring(caliste + new Vector3(-4.5f, 0.2f, 3f), 4.5f));

            EnsureSnake(
                root.transform,
                "Snake_GraveyardPath",
                graveyard + new Vector3(-3f, 0.2f, 4f),
                Ring(graveyard + new Vector3(-3f, 0.2f, 4f), 4f));

            // Foggy marsh approach — croc at the marsh gate.
            EnsureCrocodile(
                root.transform,
                "Crocodile_FoggyMarsh",
                marshGate + new Vector3(-2f, 0.2f, 6f),
                radius: 7f);

            // Lantern quest: path through the fog, then a guard at the pickup.
            EnsureSnake(
                root.transform,
                "Snake_LanternPath",
                lanternPath,
                Ring(lanternPath, 5f));

            EnsureSnake(
                root.transform,
                "Snake_LanternGuard",
                lantern + new Vector3(4.8f, 0.2f, -5.2f),
                Ring(lantern + new Vector3(4.8f, 0.2f, -5.2f), 4.2f));

            EnsureCrocodile(
                root.transform,
                "Crocodile_LanternMarsh",
                lantern + new Vector3(8.5f, 0.2f, 6f),
                radius: 6.5f);

            CreaturePlaceholderVisual.PatchAll();
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

            var body = CreateBody(group.transform, "Body", start, SnakeColor,
                height: 1.2f, radius: 0.4f, scale: new Vector3(0.7f, 0.45f, 1.2f), crocodile: false);
            body.AddComponent<CreatureSense>();
            var brain = body.AddComponent<CreatureController>();
            body.AddComponent<CreatureContactHazard>();

            var snakeItem = LoadSnakeItem();
            ApplySnakeDefaults(brain, waypoints, snakeItem);
            body.GetComponent<CreaturePlaceholderVisual>()?.Configure(SnakeColor, crocodileShape: false);

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

            var body = CreateBody(group.transform, "Body", center, CrocColor,
                height: 1.6f, radius: 0.55f, scale: new Vector3(1.1f, 0.4f, 1.8f), crocodile: true);
            body.AddComponent<CreatureSense>();
            var brain = body.AddComponent<CreatureController>();
            body.AddComponent<CreatureContactHazard>();
            ApplyCrocDefaults(brain, area);
            body.GetComponent<CreaturePlaceholderVisual>()?.Configure(CrocColor, crocodileShape: true);

            return group;
        }

        private static void EnsureSnake(Transform parent, string name, Vector3 start, Vector3[] worldWaypoints)
        {
            if (GameObject.Find(name) != null) return;
            CreateSnake(parent, name, start, worldWaypoints);
        }

        private static void EnsureCrocodile(Transform parent, string name, Vector3 center, float radius)
        {
            if (GameObject.Find(name) != null) return;
            CreateCrocodile(parent, name, center, radius);
        }

        private static GameObject CreateBody(
            Transform parent,
            string name,
            Vector3 worldPos,
            Color color,
            float height,
            float radius,
            Vector3 scale,
            bool crocodile)
        {
            GameObject body;
            if (crocodile)
            {
                body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            }
            else
            {
                // Snakes are the authored mesh on this same transform. No capsule stand-in.
                body = new GameObject(name);
                scale = Vector3.one;
            }

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

            var hostRend = body.GetComponent<MeshRenderer>();
            if (hostRend != null)
                hostRend.enabled = false;

            var visual = body.AddComponent<CreaturePlaceholderVisual>();
            visual.Configure(color, crocodileShape: crocodile);

            return body;
        }

        private static Vector3[] Ring(Vector3 center, float radius)
        {
            return new[]
            {
                center + new Vector3(radius, 0f, 0f),
                center + new Vector3(0f, 0f, radius),
                center + new Vector3(-radius, 0f, 0f),
                center + new Vector3(0f, 0f, -radius)
            };
        }

        private static Vector3 Anchor(string objectName, Vector3 fallback)
        {
            var go = GameObject.Find(objectName);
            return go != null ? go.transform.position : fallback;
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
                so.FindProperty("maxHealth").floatValue = 3f;
                so.FindProperty("meleeHitDamage").floatValue = 1f;
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
                so.FindProperty("maxHealth").floatValue = 8f;
                so.FindProperty("meleeHitDamage").floatValue = 1f;
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

        /// <summary>
        /// Animated snake meshes that were placed without a brain still patrol, sense, and lunge.
        /// </summary>
        private static void AdoptLooseAnimatedSnakes()
        {
            var animators = Object.FindObjectsByType<Animator>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < animators.Length; i++)
            {
                var anim = animators[i];
                if (anim == null || anim.runtimeAnimatorController == null) continue;
                if (anim.runtimeAnimatorController.name != "Snake") continue;
                if (anim.GetComponentInParent<CreatureController>() != null) continue;

                var host = anim.gameObject;
                anim.applyRootMotion = false;
                if (host.GetComponent<CreatureSense>() == null)
                    host.AddComponent<CreatureSense>();
                var brain = host.GetComponent<CreatureController>();
                if (brain == null)
                    brain = host.AddComponent<CreatureController>();
                if (host.GetComponent<CreatureContactHazard>() == null)
                    host.AddComponent<CreatureContactHazard>();

                var origin = host.transform.position;
                var holder = new GameObject(host.name + "_Waypoints");
                var waypoints = new Transform[4];
                var offsets = new[]
                {
                    new Vector3(4.2f, 0f, 0f),
                    new Vector3(0f, 0f, 4.2f),
                    new Vector3(-4.2f, 0f, 0f),
                    new Vector3(0f, 0f, -4.2f)
                };
                for (var w = 0; w < offsets.Length; w++)
                {
                    var wp = new GameObject($"WP_{w + 1}");
                    wp.transform.SetParent(holder.transform, false);
                    wp.transform.position = origin + offsets[w];
                    waypoints[w] = wp.transform;
                }

                ApplySnakeDefaults(brain, waypoints, LoadSnakeItem());
                host.GetComponent<CreaturePlaceholderVisual>()?.Configure(SnakeColor, crocodileShape: false);
            }
        }

        private static void RemoveExisting()
        {
            var root = GameObject.Find(RootName);
            if (root != null)
                Object.DestroyImmediate(root);
        }
    }
}
