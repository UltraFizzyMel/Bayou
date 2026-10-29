using Bayou.Inventory;
using Bayou.Player;
using Bayou.Rendering;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Bayou.Environment
{
    /// <summary>
    /// Drops a readable sign and Caliste's box near the player when a scene has neither.
    /// </summary>
    public static class WorldInteractBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Ensure();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Ensure();

        public static void Ensure()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid()) return;
            if (string.Equals(scene.name, "MainMenu", System.StringComparison.OrdinalIgnoreCase))
                return;

            var player = Object.FindFirstObjectByType<BayouCharacterMotor>();
            var origin = player != null ? player.transform.position : Vector3.zero;

            WirePlacedSigns();

            if (GameObject.Find("Sign_BayouMarker") == null)
                CreateSign(origin + new Vector3(-2.6f, 0f, 2.4f));

            if (GameObject.Find("Pickup_CalistesBox") == null)
                CreateBox(origin + new Vector3(2.4f, 0f, 1.8f));

            if (Object.FindFirstObjectByType<SignWireRunner>() == null)
            {
                var runner = new GameObject("SignWire");
                runner.AddComponent<SignWireRunner>();
            }
        }

        private sealed class SignWireRunner : MonoBehaviour
        {
            private void Start() => WirePlacedSigns();
        }

        /// <summary>
        /// Scene sign posts are static meshes. Give each one a reader so E works
        /// without editing the prefab.
        /// </summary>
        public static void WirePlacedSigns()
        {
            var transforms = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
            for (var i = 0; i < transforms.Length; i++)
            {
                var t = transforms[i];
                if (t == null) continue;
                if (!TryCopyForSign(t.name, out var title, out var body)) continue;
                if (t.GetComponent<ReadableSign>() != null) continue;
                if (HasSignAncestor(t)) continue;

                var sign = t.gameObject.AddComponent<ReadableSign>();
                sign.Configure(title, body, "Read");
            }
        }

        private static bool TryCopyForSign(string objectName, out string title, out string body)
        {
            title = null;
            body = null;
            if (string.IsNullOrEmpty(objectName)) return false;

            if (objectName.StartsWith("SignOne") || objectName.StartsWith("Sign 1"))
            {
                title = "Church road";
                body = "White posts mark the church road.\nDon't follow lights off the path after dark.";
                return true;
            }

            if (objectName.StartsWith("SignTwo") || objectName.StartsWith("Sign 2"))
            {
                title = "Foggy marsh";
                body = "Crocodiles own this water.\nA net stuns them. It does not kill them.";
                return true;
            }

            if (objectName.StartsWith("SignThree") || objectName.StartsWith("Sign 3"))
            {
                title = "Graveyard path";
                body = "The graveyard path leaves the bank here.\nIf the water goes still, step back.";
                return true;
            }

            return false;
        }

        private static bool HasSignAncestor(Transform t)
        {
            var parent = t.parent;
            while (parent != null)
            {
                if (TryCopyForSign(parent.name, out _, out _))
                    return true;
                parent = parent.parent;
            }

            return false;
        }

        public static GameObject CreateSign(Vector3 position)
        {
            var root = new GameObject("Sign_BayouMarker");
            root.transform.position = position;
            CreatureSnap(root.transform);

            var board = GameObject.CreatePrimitive(PrimitiveType.Cube);
            board.name = "Board";
            board.transform.SetParent(root.transform, false);
            board.transform.localPosition = new Vector3(0f, 0.85f, 0f);
            board.transform.localScale = new Vector3(0.12f, 1.15f, 0.72f);
            var rend = board.GetComponent<Renderer>();
            if (rend != null)
                rend.sharedMaterial = BayouShaderUtil.CreateUnlitColor(new Color(0.42f, 0.36f, 0.24f, 1f));

            var trigger = root.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 1.8f;
            trigger.center = new Vector3(0f, 0.6f, 0f);

            var sign = root.AddComponent<ReadableSign>();
            sign.Configure(
                "Mile marker",
                "East of here the water belongs to the crocodiles. They do not die. A thrown net stuns one long enough to slip past.\n\nPress E again to close this.");
            return root;
        }

        public static GameObject CreateBox(Vector3 position)
        {
            var root = new GameObject("Pickup_CalistesBox");
            root.transform.position = position;
            CreatureSnap(root.transform);

            var mesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mesh.name = "Box";
            mesh.transform.SetParent(root.transform, false);
            mesh.transform.localPosition = new Vector3(0f, 0.22f, 0f);
            mesh.transform.localScale = new Vector3(0.42f, 0.32f, 0.42f);
            var rend = mesh.GetComponent<Renderer>();
            if (rend != null)
                rend.sharedMaterial = BayouShaderUtil.CreateUnlitColor(new Color(0.48f, 0.3f, 0.14f, 1f));

            var trigger = root.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 1.6f;
            trigger.center = new Vector3(0f, 0.4f, 0f);

            var pickup = root.AddComponent<ShinyWorldPickup>();
            pickup.Bind(LoadBox(), "Pick up");
            return root;
        }

        private static ItemDefinition LoadBox()
        {
            var loaded = Resources.Load<ItemDefinition>("Bayou/Items/Item_Box");
            if (loaded != null) return loaded;
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<ItemDefinition>(
                "Assets/Inventory/Items/Item_Box.asset");
#else
            return null;
#endif
        }

        private static void CreatureSnap(Transform t)
        {
            Bayou.Creatures.CreatureController.SnapBodyToGround(t, 0.05f);
        }
    }
}
