using UnityEngine;

namespace Bayou.Fishing
{
    /// <summary>
    /// Enables a point light while the lantern is the active held item.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HeldLantern : MonoBehaviour
    {
        [SerializeField] private Light lanternLight;
        [SerializeField] private float intensity = 2.2f;
        [SerializeField] private float range = 12f;
        [SerializeField] private Color color = new(1f, 0.82f, 0.55f, 1f);

        private void Awake()
        {
            EnsureLight();
            SetLit(false);
        }

        public void SetLit(bool on)
        {
            EnsureLight();
            if (lanternLight != null)
                lanternLight.enabled = on;
        }

        private void EnsureLight()
        {
            if (lanternLight != null) return;
            lanternLight = GetComponentInChildren<Light>(true);
            if (lanternLight != null) return;

            var go = new GameObject("LanternLight");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0.15f, 0.1f);
            lanternLight = go.AddComponent<Light>();
            lanternLight.type = LightType.Point;
            lanternLight.intensity = intensity;
            lanternLight.range = range;
            lanternLight.color = color;
            lanternLight.shadows = LightShadows.Soft;
        }
    }
}
