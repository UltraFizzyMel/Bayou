using Bayou.Inventory;
using Bayou.Quests;
using UnityEngine;

namespace Bayou.Rendering
{
    /// <summary>
    /// URP-safe placeholder meshes for pickups and held gear. Built-in Default-Diffuse
    /// and Shader.Find materials vanish in player builds.
    /// </summary>
    public static class WorldItemVisual
    {
        public static void PatchScenePickups()
        {
            var pickups = Object.FindObjectsByType<QuestItemPickup>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < pickups.Length; i++)
            {
                var pickup = pickups[i];
                if (pickup == null) continue;
                EnsurePickupVisual(pickup.gameObject, pickup.Item);
                SnapToGround(pickup.transform, 0.22f);
            }

            var shinies = Object.FindObjectsByType<PondShinyCollectible>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < shinies.Length; i++)
            {
                var shiny = shinies[i];
                if (shiny == null) continue;
                PatchRenderers(shiny.gameObject, force: true);
            }
        }

        public static void PatchRenderers(GameObject root, bool force = false)
        {
            if (root == null) return;
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null) continue;
                var mat = r.sharedMaterial;
                if (!force && mat != null && mat.shader != null && !IsBrokenShader(mat.shader))
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
            PatchRenderers(root, force: true);
            if (item == null) return;

            var id = item.Id ?? item.name;
            if (IsLantern(id))
                BuildLantern(root.transform, replaceExisting: true);
            else if (IsNet(id))
                BuildNet(root.transform, replaceExisting: true);
        }

        public static GameObject BuildLantern(Transform parent, bool replaceExisting)
        {
            var existing = parent.Find("LanternVisual");
            if (existing != null)
            {
                PatchRenderers(existing.gameObject, force: true);
                HideHostMesh(parent);
                return existing.gameObject;
            }

            var root = new GameObject("LanternVisual");
            root.transform.SetParent(parent, false);
            NeutralizeParentScale(root.transform);
            root.transform.localPosition = new Vector3(0f, 0.15f, 0f);
            root.transform.localRotation = Quaternion.identity;

            Primitive(root.transform, PrimitiveType.Cylinder, "Body",
                new Vector3(0f, 0.08f, 0f), new Vector3(0.38f, 0.22f, 0.38f),
                new Color(0.42f, 0.26f, 0.12f, 1f));
            Primitive(root.transform, PrimitiveType.Sphere, "Glass",
                new Vector3(0f, 0.32f, 0f), new Vector3(0.32f, 0.36f, 0.32f),
                new Color(1f, 0.82f, 0.38f, 0.92f));
            Primitive(root.transform, PrimitiveType.Capsule, "Handle",
                new Vector3(0f, 0.58f, 0f), new Vector3(0.08f, 0.16f, 0.08f),
                new Color(0.28f, 0.28f, 0.3f, 1f));
            HideHostMesh(parent);
            return root;
        }

        public static GameObject BuildNet(Transform parent, bool replaceExisting)
        {
            var leftover = parent.Find("NetVisual");
            if (leftover != null)
                leftover.gameObject.SetActive(false);

            var built = parent.Find("NetPickupVisual");
            if (built != null)
            {
                PatchRenderers(built.gameObject, force: true);
                HideHostMesh(parent);
                return built.gameObject;
            }

            var root = new GameObject("NetPickupVisual");
            root.transform.SetParent(parent, false);
            NeutralizeParentScale(root.transform);
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;

            Primitive(root.transform, PrimitiveType.Cylinder, "Hoop",
                new Vector3(0f, 0.18f, 0f), new Vector3(0.7f, 0.04f, 0.7f),
                new Color(0.18f, 0.42f, 0.36f, 1f));
            Primitive(root.transform, PrimitiveType.Sphere, "Bag",
                new Vector3(0f, -0.02f, 0f), new Vector3(0.55f, 0.28f, 0.55f),
                new Color(0.28f, 0.55f, 0.52f, 0.85f));
            Primitive(root.transform, PrimitiveType.Cube, "Handle",
                new Vector3(0f, 0.12f, -0.48f), new Vector3(0.08f, 0.08f, 0.62f),
                new Color(0.38f, 0.24f, 0.12f, 1f));
            HideHostMesh(parent);
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

        public static void SnapToGround(Transform t, float extraY)
        {
            if (t == null) return;
            var origin = t.position + Vector3.up * 10f;
            if (!Physics.Raycast(origin, Vector3.down, out var hit, 50f, ~0, QueryTriggerInteraction.Ignore))
                return;
            t.position = hit.point + Vector3.up * extraY;
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
                   n == "Standard" ||
                   n == "Sprites/Default";
        }

        private static void HideHostMesh(Transform parent)
        {
            var rend = parent.GetComponent<MeshRenderer>();
            if (rend != null)
                rend.enabled = false;
        }

        private static void NeutralizeParentScale(Transform child)
        {
            var ls = child.parent != null ? child.parent.localScale : Vector3.one;
            child.localScale = new Vector3(
                1f / Mathf.Max(0.05f, Mathf.Abs(ls.x)),
                1f / Mathf.Max(0.05f, Mathf.Abs(ls.y)),
                1f / Mathf.Max(0.05f, Mathf.Abs(ls.z)));
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
            {
                if (Application.isPlaying)
                    Object.Destroy(col);
                else
                    Object.DestroyImmediate(col);
            }

            Tint(go, color);
            return go;
        }
    }
}
