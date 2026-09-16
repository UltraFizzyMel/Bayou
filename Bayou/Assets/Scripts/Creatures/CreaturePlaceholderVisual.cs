using UnityEngine;

namespace Bayou.Creatures
{
    /// <summary>Applies a solid placeholder color at runtime so baked creatures stay visible in builds.</summary>
    [DisallowMultipleComponent]
    public sealed class CreaturePlaceholderVisual : MonoBehaviour
    {
        [SerializeField] private Color color = new(0.35f, 0.75f, 0.3f);

        private static readonly Color HurtFlash = new(1f, 0.35f, 0.28f, 1f);
        private Material _mat;
        private float _flashUntil;
        private float _health01 = 1f;

        private void Awake()
        {
            Apply();
        }

        private void OnDestroy()
        {
            if (_mat != null)
                Destroy(_mat);
        }

        public void Configure(Color c)
        {
            color = c;
            Apply();
        }

        public void NotifyHealth(float current, float max)
        {
            _health01 = max > 0.01f ? Mathf.Clamp01(current / max) : 0f;
            if (Time.time >= _flashUntil)
                ApplyTint(HealthTint());
        }

        public void FlashHurt()
        {
            _flashUntil = Time.time + 0.12f;
            ApplyTint(HurtFlash);
        }

        private void Update()
        {
            if (_flashUntil <= 0f || Time.time < _flashUntil) return;
            _flashUntil = 0f;
            ApplyTint(HealthTint());
        }

        private Color HealthTint()
        {
            if (_health01 >= 0.999f) return color;
            return Color.Lerp(new Color(0.45f, 0.12f, 0.1f, 1f), color, _health01);
        }

        private void Apply()
        {
            var rend = GetComponent<Renderer>();
            if (rend == null) return;
            if (_mat == null)
                _mat = Bayou.Rendering.BayouShaderUtil.CreateUnlitColor(color);
            rend.sharedMaterial = _mat;
            ApplyTint(color);
        }

        private void ApplyTint(Color c)
        {
            if (_mat == null) Apply();
            if (_mat == null) return;
            if (_mat.HasProperty("_BaseColor")) _mat.SetColor("_BaseColor", c);
            if (_mat.HasProperty("_Color")) _mat.SetColor("_Color", c);
        }
    }
}
