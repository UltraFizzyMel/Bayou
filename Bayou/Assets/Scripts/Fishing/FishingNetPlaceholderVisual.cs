using UnityEngine;

namespace Bayou.Fishing
{
    /// <summary>
    /// "Almost no art" net: invisible physics body + a motion streak (TrailRenderer).
    /// Put on the same GameObject as Rigidbody, Collider, and FishingNetProjectile.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FishingNetPlaceholderVisual : MonoBehaviour
    {
        [Header("Invisible body")]
        [Tooltip("Turn off MeshRenderer / SkinnedMeshRenderer on this object and children (e.g. a dev-only cube).")]
        [SerializeField] private bool disableMeshRenderers = true;

        [Header("Streak")]
        [SerializeField] private bool addTrailIfMissing = true;
        [SerializeField] private float trailTime = 0.35f;
        [SerializeField] private float startWidth = 0.22f;
        [SerializeField] private float endWidth = 0.04f;
        [SerializeField] private float minVertexDistance = 0.025f;
        [SerializeField] private Color trailColor = new(0.35f, 0.9f, 1f, 0.9f);

        [Header("Stop streak when net sticks (optional)")]
        [SerializeField] private bool stopEmittingWhenKinematic = true;

        private TrailRenderer trail;
        private Rigidbody rb;

        private void Reset()
        {
            ConfigureIfNeeded();
        }

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            if (disableMeshRenderers) HideMeshes();
            ConfigureIfNeeded();
        }

        private void LateUpdate()
        {
            if (!stopEmittingWhenKinematic || trail == null) return;
            if (rb != null && rb.isKinematic && trail.emitting) trail.emitting = false;
        }

        private void HideMeshes()
        {
            // Keep FishingNetVisual meshes — only hide leftover art cubes on the prefab root.
            foreach (var r in GetComponentsInChildren<MeshRenderer>(true))
            {
                if (r.GetComponentInParent<FishingNetVisual>() != null) continue;
                r.enabled = false;
            }

            foreach (var r in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (r.GetComponentInParent<FishingNetVisual>() != null) continue;
                r.enabled = false;
            }
        }

        private void ConfigureIfNeeded()
        {
            trail = GetComponent<TrailRenderer>();
            if (trail == null && addTrailIfMissing)
                trail = gameObject.AddComponent<TrailRenderer>();

            if (trail == null) return;

            trail.time = trailTime;
            trail.minVertexDistance = minVertexDistance;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;

            trail.widthCurve = AnimationCurve.Linear(0f, startWidth, 1f, endWidth);

            trail.material = CreateTrailMaterial(trailColor);

            trail.emitting = true;
        }

        private static Material CreateTrailMaterial(Color color)
        {
            var shader =
                Shader.Find("Universal Render Pipeline/Particles/Unlit")
                ?? Shader.Find("Particles/Standard Unlit")
                ?? Shader.Find("Sprites/Default")
                ?? Shader.Find("Unlit/Color");

            var mat = new Material(shader);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            else if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            else if (mat.HasProperty("_TintColor")) mat.SetColor("_TintColor", color);

            return mat;
        }
    }
}
