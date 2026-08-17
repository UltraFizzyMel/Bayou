using UnityEngine;

namespace Bayou.Creatures
{
    /// <summary>Applies a solid placeholder color at runtime so baked creatures stay visible in builds.</summary>
    [DisallowMultipleComponent]
    public sealed class CreaturePlaceholderVisual : MonoBehaviour
    {
        [SerializeField] private Color color = new(0.35f, 0.75f, 0.3f);

        private void Awake()
        {
            Apply();
        }

        public void Configure(Color c)
        {
            color = c;
            Apply();
        }

        private void Apply()
        {
            var rend = GetComponent<Renderer>();
            if (rend == null) return;
            rend.sharedMaterial = Bayou.Rendering.BayouShaderUtil.CreateUnlitColor(color);
        }
    }
}
