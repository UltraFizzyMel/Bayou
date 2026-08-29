using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Bayou.Fishing
{
    /// <summary>
    /// Held lantern: lights the area, punches through fog, and marks the player as lit.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HeldLantern : MonoBehaviour
    {
        [SerializeField] private Light lanternLight;
        [SerializeField] private float intensity = 6.5f;
        [SerializeField] private float range = 18f;
        [SerializeField] private Color color = new(1f, 0.82f, 0.55f, 1f);
        [SerializeField] private float fogDensityMultiplier = 0.28f;
        [SerializeField] private float flickerAmount = 0.18f;

        private bool _lit;
        private Renderer _glowRenderer;
        private Material _glowMat;
        private float _savedFogDensity;
        private bool _savedFog;
        private bool _fogStored;
        private static HeldLantern _activeLit;

        /// <summary>True while any held lantern is currently shining.</summary>
        public static bool IsAnyLit => _activeLit != null && _activeLit._lit;

        private void Awake()
        {
            EnsureLight();
            EnsureGlow();
            SetLit(false);
        }

        private void OnDisable()
        {
            if (_lit)
                SetLit(false);
        }

        private void OnDestroy()
        {
            if (_activeLit == this)
                RestoreFog();
            if (_glowMat != null)
                Destroy(_glowMat);
        }

        private void Update()
        {
            if (!_lit || lanternLight == null) return;
            var flicker = 1f + Mathf.Sin(Time.time * 9.3f) * flickerAmount * 0.5f
                          + Mathf.Sin(Time.time * 17.1f) * flickerAmount * 0.35f;
            lanternLight.intensity = intensity * flicker;
            if (_glowRenderer != null && _glowMat != null)
            {
                var c = color;
                c.a = 0.55f + 0.25f * flicker;
                if (_glowMat.HasProperty("_BaseColor"))
                    _glowMat.SetColor("_BaseColor", c);
                if (_glowMat.HasProperty("_Color"))
                    _glowMat.SetColor("_Color", c);
            }
        }

        public void SetLit(bool on)
        {
            EnsureLight();
            EnsureGlow();
            _lit = on;

            if (lanternLight != null)
                lanternLight.enabled = on;
            if (_glowRenderer != null)
                _glowRenderer.enabled = on;

            if (on)
            {
                _activeLit = this;
                CaptureFog();
                if (RenderSettings.fog)
                    RenderSettings.fogDensity = _savedFogDensity * Mathf.Clamp01(fogDensityMultiplier);
            }
            else
            {
                if (_activeLit == this)
                    RestoreFog();
            }
        }

        private void CaptureFog()
        {
            if (_fogStored) return;
            _savedFog = RenderSettings.fog;
            _savedFogDensity = RenderSettings.fogDensity;
            _fogStored = true;
        }

        private void RestoreFog()
        {
            if (_activeLit == this)
                _activeLit = null;
            if (!_fogStored) return;
            RenderSettings.fog = _savedFog;
            RenderSettings.fogDensity = _savedFogDensity;
        }

        private void EnsureLight()
        {
            if (lanternLight != null)
            {
                ConfigureLight(lanternLight);
                return;
            }

            lanternLight = GetComponentInChildren<Light>(true);
            if (lanternLight == null)
            {
                var go = new GameObject("LanternLight");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(0f, 0.15f, 0.1f);
                lanternLight = go.AddComponent<Light>();
            }

            ConfigureLight(lanternLight);
        }

        private void ConfigureLight(Light light)
        {
            light.type = LightType.Point;
            light.intensity = intensity;
            light.range = range;
            light.color = color;
            light.shadows = LightShadows.Soft;
            if (light.GetComponent<UniversalAdditionalLightData>() == null)
                light.gameObject.AddComponent<UniversalAdditionalLightData>();
        }

        private void EnsureGlow()
        {
            if (_glowRenderer != null) return;
            var existing = transform.Find("LanternGlow");
            GameObject glowGo;
            if (existing != null)
            {
                glowGo = existing.gameObject;
            }
            else
            {
                glowGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                glowGo.name = "LanternGlow";
                glowGo.transform.SetParent(transform, false);
                glowGo.transform.localPosition = new Vector3(0f, 0.12f, 0f);
                glowGo.transform.localScale = Vector3.one * 0.28f;
                var col = glowGo.GetComponent<Collider>();
                if (col != null) Destroy(col);
            }

            _glowRenderer = glowGo.GetComponent<MeshRenderer>();
            if (_glowRenderer != null)
            {
                _glowMat = Bayou.Rendering.BayouShaderUtil.CreateUnlitColor(
                    new Color(color.r, color.g, color.b, 0.7f));
                _glowRenderer.sharedMaterial = _glowMat;
                _glowRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }
    }
}
