using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Bayou.Fishing
{
    /// <summary>
    /// Held lantern: a world-space point light snapped to the lantern glass so the
    /// glow actually comes from the hand, not the player root.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    public sealed class HeldLantern : MonoBehaviour
    {
        [SerializeField] private Light lanternLight;
        [SerializeField] private float intensity = 22f;
        [SerializeField] private float range = 8f;
        [SerializeField] private Color color = new(1f, 0.78f, 0.48f, 1f);
        [SerializeField] private float flickerAmount = 0.16f;

        private bool _lit;
        private Renderer _glowRenderer;
        private Material _glowMat;
        private Transform _emitter;
        private static HeldLantern _activeLit;

        /// <summary>True while any held lantern is currently shining.</summary>
        public static bool IsAnyLit => _activeLit != null && _activeLit._lit;

        private void Awake()
        {
            EnsureEmitter();
            EnsureLight();
            EnsureGlow();
            SetLit(false);
        }

        private void OnEnable()
        {
            if (_lit)
                SetLit(true);
        }

        private void OnDisable()
        {
            if (lanternLight != null)
                lanternLight.enabled = false;
        }

        private void OnDestroy()
        {
            if (_activeLit == this)
                _activeLit = null;
            if (_glowMat != null)
                Destroy(_glowMat);
            if (lanternLight != null && lanternLight.gameObject != gameObject)
                Destroy(lanternLight.gameObject);
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
                c.a = 0.7f + 0.25f * flicker;
                if (_glowMat.HasProperty("_BaseColor"))
                    _glowMat.SetColor("_BaseColor", c);
                if (_glowMat.HasProperty("_Color"))
                    _glowMat.SetColor("_Color", c);
            }
        }

        private void LateUpdate()
        {
            if (!_lit || lanternLight == null) return;
            SnapLightToHand();
        }

        public void SetLit(bool on)
        {
            EnsureEmitter();
            EnsureLight();
            EnsureGlow();
            _lit = on && isActiveAndEnabled;

            if (lanternLight != null)
            {
                lanternLight.enabled = _lit;
                if (_lit)
                    SnapLightToHand();
            }
            if (_glowRenderer != null)
                _glowRenderer.enabled = _lit;

            if (_lit)
                _activeLit = this;
            else if (_activeLit == this)
                _activeLit = null;
        }

        private void SnapLightToHand()
        {
            var pos = EmitterWorldPos();
            var lightT = lanternLight.transform;
            if (lightT.parent != null)
                lightT.SetParent(null, true);
            lightT.position = pos;
            lightT.rotation = Quaternion.identity;
            lightT.localScale = Vector3.one;
        }

        private Vector3 EmitterWorldPos()
        {
            EnsureEmitter();
            return _emitter != null ? _emitter.position : transform.position;
        }

        private void EnsureEmitter()
        {
            if (_emitter != null) return;
            _emitter = transform.Find("Glass");
            if (_emitter == null)
            {
                var glow = transform.Find("LanternGlow");
                if (glow != null)
                    _emitter = glow;
            }
            if (_emitter == null)
                _emitter = transform;
        }

        private void EnsureLight()
        {
            DestroyLeftoverBodyLights();

            if (lanternLight == null)
                lanternLight = GetComponentInChildren<Light>(true);

            if (lanternLight == null)
            {
                var go = new GameObject("LanternWorldLight");
                lanternLight = go.AddComponent<Light>();
            }

            // Keep the light unparented so a scaled hand bone cannot swallow it.
            if (lanternLight.transform.parent != null)
                lanternLight.transform.SetParent(null, true);
            lanternLight.transform.localScale = Vector3.one;
            ConfigureLight(lanternLight);
        }

        private void DestroyLeftoverBodyLights()
        {
            var eq = GetComponentInParent<BayouFishingEquipment>();
            var host = eq != null ? eq.transform : transform.root;
            if (host == null) return;
            for (var i = host.childCount - 1; i >= 0; i--)
            {
                var child = host.GetChild(i);
                if (child == null) continue;
                if (child.name != "LanternLight" && child.name != "LanternWorldLight")
                    continue;
                if (lanternLight != null && child.gameObject == lanternLight.gameObject)
                    continue;
                Object.Destroy(child.gameObject);
            }
        }

        private void ConfigureLight(Light light)
        {
            light.type = LightType.Point;
            light.intensity = intensity;
            light.range = range;
            light.color = color;
            light.shadows = LightShadows.None;
            light.renderMode = LightRenderMode.ForcePixel;
            light.cullingMask = ~0;
            light.renderingLayerMask = int.MaxValue;

            var data = light.GetComponent<UniversalAdditionalLightData>() ??
                       light.gameObject.AddComponent<UniversalAdditionalLightData>();
            data.usePipelineSettings = true;
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
                glowGo.transform.localScale = Vector3.one * 0.22f;
                var col = glowGo.GetComponent<Collider>();
                if (col != null) Destroy(col);
            }

            _glowRenderer = glowGo.GetComponent<MeshRenderer>();
            if (_glowRenderer != null)
            {
                _glowMat = Bayou.Rendering.BayouShaderUtil.CreateUnlitColor(
                    new Color(color.r, color.g, color.b, 0.85f));
                _glowRenderer.sharedMaterial = _glowMat;
                _glowRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }
    }
}
