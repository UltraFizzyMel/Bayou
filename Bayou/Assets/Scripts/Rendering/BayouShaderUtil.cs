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
                if (template != null && template.shader != null)
                {
                    _cachedUnlitTemplate = template;
                    _cachedUnlit = template.shader;
                    return _cachedUnlit;
                }

                _cachedUnlit =
                    Shader.Find("Universal Render Pipeline/Unlit") ??
                    Shader.Find("Universal Render Pipeline/Lit") ??
                    Shader.Find("Sprites/Default") ??
                    Shader.Find("Unlit/Color") ??
                    Shader.Find("Hidden/InternalErrorShader");

                return _cachedUnlit;
            }
        }

        public static Material CreateUnlitColor(Color color)
        {
            var shader = Unlit;
            if (shader == null)
            {
                Debug.LogError("[Bayou] No usable unlit shader in build — check Always Included / Resources.");
                return new Material(Shader.Find("Hidden/InternalErrorShader") ?? Shader.Find("Sprites/Default"));
            }

            Material mat;
            if (_cachedUnlitTemplate != null)
                mat = new Material(_cachedUnlitTemplate);
            else
                mat = new Material(shader);

            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            return mat;
        }
    }
}
