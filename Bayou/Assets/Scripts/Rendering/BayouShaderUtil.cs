using UnityEngine;

namespace Bayou.Rendering
{
    /// <summary>
    /// Resolves shaders safely in player builds (Shader.Find often returns null when stripped).
    /// Prefers a Resources material so URP Unlit is always included in the build.
    /// </summary>
    public static class BayouShaderUtil
    {
        private const string ResourcesUnlitMat = "Bayou/RuntimeUnlit";
        private static Shader _cachedUnlit;
        private static Material _cachedUnlitTemplate;

        public static Shader Unlit
        {
            get
            {
                if (_cachedUnlit != null) return _cachedUnlit;

                var template = Resources.Load<Material>(ResourcesUnlitMat);
                if (template != null && IsUsable(template.shader))
                {
                    _cachedUnlitTemplate = template;
                    _cachedUnlit = template.shader;
                    return _cachedUnlit;
                }

                _cachedUnlitTemplate = null;
                _cachedUnlit =
                    Shader.Find("Sprites/Default") ??
                    Shader.Find("UI/Default") ??
                    Shader.Find("Universal Render Pipeline/Unlit") ??
                    Shader.Find("Universal Render Pipeline/Lit") ??
                    Shader.Find("Unlit/Color");

                if (!IsUsable(_cachedUnlit))
                    _cachedUnlit = null;

                return _cachedUnlit;
            }
        }

        public static Material CreateUnlitColor(Color color)
        {
            var shader = Unlit;
            if (!IsUsable(shader))
            {
                Debug.LogError("[Bayou] No usable unlit shader in build — check Always Included / Resources.");
                return null;
            }

            Material mat;
            if (_cachedUnlitTemplate != null)
                mat = new Material(_cachedUnlitTemplate);
            else
                mat = new Material(shader);

            ApplyColor(mat, color);
            return mat;
        }

        private static bool IsUsable(Shader shader)
        {
            if (shader == null) return false;
            var name = shader.name;
            if (string.IsNullOrEmpty(name)) return false;
            return name.IndexOf("InternalError", System.StringComparison.OrdinalIgnoreCase) < 0;
        }

        public static void ApplyColor(Material mat, Color color)
        {
            if (mat == null) return;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            if (mat.HasProperty("_UnlitColor")) mat.SetColor("_UnlitColor", color);
            if (mat.HasProperty("_EmissiveColor")) mat.SetColor("_EmissiveColor", color * 0.35f);
            if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", color * 0.25f);
        }
    }
}
