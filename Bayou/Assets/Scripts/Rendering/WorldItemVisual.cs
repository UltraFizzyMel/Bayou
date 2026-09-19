using Bayou.Inventory;
using UnityEngine;

namespace Bayou.Rendering
{
    /// <summary>
    /// URP-safe placeholder meshes for pickups and held gear. Built-in Default-Diffuse
    /// and Shader.Find materials vanish in player builds.
    /// </summary>
    public static class WorldItemVisual
    {
        public static void PatchRenderers(GameObject root)
        {
            if (root == null) return;
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null) continue;
                var mat = r.sharedMaterial;
                if (mat != null && mat.shader != null && !IsBrokenShader(mat.shader))
                    continue;

                var color = mat != null && mat.HasProperty("_Color")
                    ? mat.color
                    : new Color(0.85f, 0.72f, 0.35f, 1f);
                r.sharedMaterial = BayouShaderUtil.CreateUnlitColor(color);
                r.enabled = true;
            }
        }

        public static void EnsurePickupVisual(GameObject root, ItemDefinition item)
        {
            if (root == null) return;
            PatchRenderers(root);
            if (item == null) return;

            var id = item.Id ?? item.name;
            if (IsLantern(id))
                BuildLantern(root.transform, replaceExisting: false);
            else if (IsNet(id))
                BuildNet(root.transform, replaceExisting: false);
        }

        public static GameObject BuildLantern(Transform parent, bool replaceExisting)
        {
            var existing = parent.Find("LanternVisual");
            if (existing != null)
            {
                PatchRenderers(existing.gameObject);
                return existing.gameObject;
            }

            HideHostMesh(parent);
            var root = new GameObject("LanternVisual");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(0f, 0.15f, 0f);
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;

            var body = Primitive(root.transform, PrimitiveType.Cylinder, "Body",
                new Vector3(0f, 0.08f, 0f), new Vector3(0.38f, 0.22f, 0.38f),
                new Color(0.42f, 0.26f, 0.12f, 1f));
            Primitive(root.transform, PrimitiveType.Sphere, "Glass",
                new Vector3(0f, 0.32f, 0f), new Vector3(0.32f, 0.36f, 0.32f),
                new Color(1f, 0.82f, 0.38f, 0.92f));
            Primitive(root.transform, PrimitiveType.Capsule, "Handle",
                new Vector3(0f, 0.58f, 0f), new Vector3(0.08f, 0.16f, 0.08f),
                new Color(0.28f, 0.28f, 0.3f, 1f));
            _ = body;
            return root;
        }

        public static GameObject BuildNet(Transform parent, bool replaceExisting)
        {
            var existing = parent.Find("NetVisual");
            if (existing != null)
            {
                // Pond editor placeholder — leave it, just make sure children render.
                if (!replaceExisting)
                {
                    PatchRenderers(existing.gameObject);
                    return existing.gameObject;
                }
            }

            var built = parent.Find("NetPickupVisual");
            if (built != null)
            {
                PatchRenderers(built.gameObject);
                return built.gameObject;
            }

            HideHostMesh(parent);
            var root = new GameObject("NetPickupVisual");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;

            Primitive(root.transform, PrimitiveType.Cylinder, "Hoop",
                new Vector3(0f, 0.18f, 0f), new Vector3(0.7f, 0.04f, 0.7f),
                new Color(0.18f, 0.42f, 0.36f, 1f));
            Primitive(root.transform, PrimitiveType.Sphere, "Bag",
                new Vector3(0f, -0.02f, 0f), new Vector3(0.55f, 0.28f, 0.55f),
                new Color(0.28f, 0.55f, 0.52f, 0.85f));
            Primitive(root.transform, PrimitiveType.Cube, "Handle",
                new Vector3(0f, 0.12f, -0.48f), new Vector3(0.08f, 0.08f, 0.62f),
                new Color(0.38f, 0.24f, 0.12f, 1f));
            return root;
        }

        public static void Tint(GameObject go, Color color)
        {
            if (go == null) return;
            var rend = go.GetComponent<MeshRenderer>();
            if (rend == null) return;
            rend.sharedMaterial = BayouShaderUtil.CreateUnlitColor(color);
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.enabled = true;
        }

        public static bool IsLantern(string id) =>
            !string.IsNullOrWhiteSpace(id) &&
            id.IndexOf("Lantern", System.StringComparison.OrdinalIgnoreCase) >= 0;

        public static bool IsNet(string id) =>
            !string.IsNullOrWhiteSpace(id) &&
            (id.IndexOf("HandNet", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
             string.Equals(id, "Item_Net", System.StringComparison.OrdinalIgnoreCase));

        private static bool IsBrokenShader(Shader shader)
        {
            if (shader == null) return true;
            var n = shader.name;
            return n.IndexOf("Hidden/InternalError", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   n.IndexOf("Legacy Shaders", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   n == "Diffuse" ||
                   n == "Specular" ||
                   n == "Standard";
        }

        private static void HideHostMesh(Transform parent)
        {
            var filter = parent.GetComponent<MeshFilter>();
            var rend = parent.GetComponent<MeshRenderer>();
            if (rend != null)
                rend.enabled = false;
            _ = filter;
        }

        private static GameObject Primitive(
            Transform parent,
            PrimitiveType type,
            string name,
            Vector3 localPos,
            Vector3 localScale,
            Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = localScale;
            var col = go.GetComponent<Collider>();
            if (col != null)
                Object.Destroy(col);
            Tint(go, color);
            return go;
        }
    }
}
