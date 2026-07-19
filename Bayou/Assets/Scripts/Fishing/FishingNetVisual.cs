using UnityEngine;

namespace Bayou.Fishing
{
    /// <summary>
    /// Runtime visual for the thrown/planted fishing net (no art dependency).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FishingNetVisual : MonoBehaviour
    {
        [SerializeField] private float radius = 0.9f;
        [SerializeField] private int segments = 16;
        [SerializeField] private Color rimColor = new(0.15f, 0.45f, 0.55f, 0.95f);
        [SerializeField] private Color meshColor = new(0.25f, 0.55f, 0.6f, 0.45f);
        [SerializeField] private bool hideUntilPlanted = true;

        private GameObject _root;
        private bool _planted;

        private void Awake()
        {
            Build();
            SetVisible(!hideUntilPlanted);
        }

        public void ShowPlanted()
        {
            _planted = true;
            SetVisible(true);
        }

        public void ShowInFlight()
        {
            SetVisible(true);
        }

        private void SetVisible(bool on)
        {
            if (_root != null)
                _root.SetActive(on);
        }

        private void Build()
        {
            if (_root != null) return;

            _root = new GameObject("NetVisual");
            _root.transform.SetParent(transform, false);
            _root.transform.localPosition = Vector3.up * 0.05f;
            _root.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            // Soft disc
            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = "NetDisc";
            disc.transform.SetParent(_root.transform, false);
            disc.transform.localScale = new Vector3(radius * 2f, 0.02f, radius * 2f);
            Destroy(disc.GetComponent<Collider>());
            ApplyColor(disc, meshColor);

            // Rim ring via LineRenderer
            var rimGo = new GameObject("NetRim");
            rimGo.transform.SetParent(_root.transform, false);
            var lr = rimGo.AddComponent<LineRenderer>();
            lr.loop = true;
            lr.positionCount = segments;
            lr.useWorldSpace = false;
            lr.widthMultiplier = 0.06f;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.material = MakeUnlit(rimColor);
            for (var i = 0; i < segments; i++)
            {
                var a = (i / (float)segments) * Mathf.PI * 2f;
                lr.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f));
            }

            // Cross cords
            AddCord(_root.transform, new Vector3(-radius, 0f, 0f), new Vector3(radius, 0f, 0f), rimColor);
            AddCord(_root.transform, new Vector3(0f, -radius, 0f), new Vector3(0f, radius, 0f), rimColor);
            AddCord(_root.transform, new Vector3(-radius * 0.7f, -radius * 0.7f, 0f), new Vector3(radius * 0.7f, radius * 0.7f, 0f), rimColor);
            AddCord(_root.transform, new Vector3(-radius * 0.7f, radius * 0.7f, 0f), new Vector3(radius * 0.7f, -radius * 0.7f, 0f), rimColor);
        }

        private static void AddCord(Transform parent, Vector3 a, Vector3 b, Color color)
        {
            var go = new GameObject("Cord");
            go.transform.SetParent(parent, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.useWorldSpace = false;
            lr.widthMultiplier = 0.035f;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.material = MakeUnlit(color);
            lr.SetPosition(0, a);
            lr.SetPosition(1, b);
        }

        private static void ApplyColor(GameObject go, Color color)
        {
            var rend = go.GetComponent<MeshRenderer>();
            if (rend == null) return;
            rend.sharedMaterial = MakeUnlit(color);
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private static Material MakeUnlit(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            var mat = new Material(shader);
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);
            else if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", color);
            return mat;
        }
    }
}
