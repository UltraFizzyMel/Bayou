using UnityEngine;

namespace Bayou.Creatures
{
    /// <summary>
    /// Always-on creature mesh. Prefab capsules often have a missing material in HDRP/URP,
    /// so this builds its own colored parts instead of relying on the host renderer.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CreaturePlaceholderVisual : MonoBehaviour
    {
        private const string VisualRootName = "PlaceholderMesh";

        [SerializeField] private Color color = new(0.42f, 0.86f, 0.28f, 1f);
        [SerializeField] private bool crocodile;

        private static readonly Color HurtFlash = new(1f, 0.92f, 0.75f, 1f);
        private static readonly Color StunTint = new(0.45f, 0.75f, 1f, 1f);

        private Material _mat;
        private Renderer[] _parts;
        private bool _builtCroc;
        private float _flashUntil;
        private float _health01 = 1f;
        private float _squashUntil;
        private Vector3 _bindScale = Vector3.one;
        private bool _stunned;
        private bool _dying;
        private float _dieStarted;
        private float _coilUntil;
        private float _stretchUntil;
        private float _coilDuration = 0.3f;
        private float _stretchDuration = 0.22f;

        public static void PatchAll()
        {
            var creatures = Object.FindObjectsByType<CreatureController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < creatures.Length; i++)
            {
                var brain = creatures[i];
                if (brain == null) continue;
                var visual = brain.GetComponent<CreaturePlaceholderVisual>();
                if (visual == null)
                    visual = brain.gameObject.AddComponent<CreaturePlaceholderVisual>();
                visual.Configure(
                    brain.NetBehavior == CreatureNetBehavior.StunOnNet
                        ? new Color(0.22f, 0.42f, 0.22f, 1f)
                        : new Color(0.42f, 0.86f, 0.28f, 1f),
                    crocodileShape: brain.NetBehavior == CreatureNetBehavior.StunOnNet);
            }
        }

        private void Awake()
        {
            _bindScale = transform.localScale;
            EnsureShape();
            Apply();
        }

        private void OnDestroy()
        {
            if (_mat != null)
                Destroy(_mat);
        }

        public void Configure(Color c, bool crocodileShape = false)
        {
            color = c;
            crocodile = crocodileShape;
            EnsureShape();
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
            _flashUntil = Time.time + 0.22f;
            _squashUntil = Time.time + 0.18f;
            ApplyTint(HurtFlash);
        }

        public void SetStunned(bool stunned)
        {
            _stunned = stunned;
            if (!stunned && Time.time >= _flashUntil)
                ApplyTint(HealthTint());
        }

        public void PlayLungeCoil(float seconds)
        {
            _coilDuration = Mathf.Max(0.08f, seconds);
            _coilUntil = Time.time + _coilDuration;
            _stretchUntil = 0f;
        }

        public void PlayLungeStretch(float seconds)
        {
            _stretchDuration = Mathf.Max(0.08f, seconds);
            _stretchUntil = Time.time + _stretchDuration;
            _coilUntil = 0f;
        }

        public void PlayDeath()
        {
            _dying = true;
            _dieStarted = Time.time;
            ApplyTint(HurtFlash);
        }

        private void Update()
        {
            if (_dying)
            {
                var u = Mathf.Clamp01((Time.time - _dieStarted) / 0.28f);
                transform.localScale = Vector3.Lerp(_bindScale, _bindScale * 0.15f, u);
                var c = HurtFlash;
                c.a = 1f - u;
                ApplyTint(c);
                return;
            }

            if (_stretchUntil > Time.time)
            {
                var u = Mathf.Clamp01((_stretchUntil - Time.time) / Mathf.Max(0.05f, _stretchDuration));
                var strike = 1f - u;
                transform.localScale = new Vector3(
                    _bindScale.x * (1f - 0.18f * strike),
                    _bindScale.y * (1f - 0.08f * strike),
                    _bindScale.z * (1f + 0.7f * strike));
            }
            else if (_coilUntil > Time.time)
            {
                var u = Mathf.Clamp01((_coilUntil - Time.time) / Mathf.Max(0.05f, _coilDuration));
                var coil = 1f - u;
                transform.localScale = new Vector3(
                    _bindScale.x * (1f + 0.2f * coil),
                    _bindScale.y * (1f + 0.12f * coil),
                    _bindScale.z * (1f - 0.42f * coil));
            }
            else if (_squashUntil > 0f)
            {
                var u = Mathf.Clamp01((_squashUntil - Time.time) / 0.18f);
                var squash = 1f + 0.35f * u;
                transform.localScale = new Vector3(_bindScale.x * squash, _bindScale.y * (1f - 0.18f * u), _bindScale.z * squash);
                if (u <= 0f)
                {
                    transform.localScale = _bindScale;
                    _squashUntil = 0f;
                }
            }
            else
            {
                transform.localScale = Vector3.Lerp(transform.localScale, _bindScale, Time.deltaTime * 14f);
            }

            if (_flashUntil > 0f && Time.time >= _flashUntil)
            {
                _flashUntil = 0f;
                ApplyTint(HealthTint());
            }

            if (_stunned && Time.time >= _flashUntil)
            {
                var pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 10f);
                ApplyTint(Color.Lerp(HealthTint(), StunTint, pulse));
            }
        }

        private Color HealthTint()
        {
            if (_health01 >= 0.999f) return color;
            return Color.Lerp(new Color(0.45f, 0.12f, 0.1f, 1f), color, _health01);
        }

        private void EnsureShape()
        {
            HideBrokenHostRenderer();

            var root = transform.Find(VisualRootName);
            if (root == null)
            {
                var go = new GameObject(VisualRootName);
                go.transform.SetParent(transform, false);
                root = go.transform;
            }

            var ls = transform.localScale;
            root.localPosition = Vector3.zero;
            root.localRotation = Quaternion.identity;
            root.localScale = new Vector3(
                1f / Mathf.Max(0.05f, Mathf.Abs(ls.x)),
                1f / Mathf.Max(0.05f, Mathf.Abs(ls.y)),
                1f / Mathf.Max(0.05f, Mathf.Abs(ls.z)));

            if (root.childCount == 0 || _builtCroc != crocodile)
            {
                for (var i = root.childCount - 1; i >= 0; i--)
                    Object.DestroyImmediate(root.GetChild(i).gameObject);

                if (crocodile)
                    BuildCroc(root);
                else
                    BuildSnake(root);
                _builtCroc = crocodile;
            }

            _parts = root.GetComponentsInChildren<Renderer>(true);
        }

        private void HideBrokenHostRenderer()
        {
            var host = GetComponent<MeshRenderer>();
            if (host != null)
                host.enabled = false;
        }

        private static void BuildSnake(Transform root)
        {
            AddPart(root, PrimitiveType.Capsule, "Body", new Vector3(0f, 0.12f, 0f),
                Quaternion.Euler(90f, 0f, 0f), new Vector3(0.42f, 1.05f, 0.42f));
            AddPart(root, PrimitiveType.Sphere, "Head", new Vector3(0f, 0.18f, 1.05f),
                Quaternion.identity, Vector3.one * 0.48f);
            AddPart(root, PrimitiveType.Sphere, "Tail", new Vector3(0f, 0.1f, -1.05f),
                Quaternion.identity, Vector3.one * 0.28f);
            AddPart(root, PrimitiveType.Sphere, "EyeL", new Vector3(-0.12f, 0.32f, 1.18f),
                Quaternion.identity, Vector3.one * 0.1f);
            AddPart(root, PrimitiveType.Sphere, "EyeR", new Vector3(0.12f, 0.32f, 1.18f),
                Quaternion.identity, Vector3.one * 0.1f);
        }

        private static void BuildCroc(Transform root)
        {
            AddPart(root, PrimitiveType.Capsule, "Body", new Vector3(0f, 0.16f, 0f),
                Quaternion.Euler(90f, 0f, 0f), new Vector3(0.85f, 1.15f, 0.55f));
            AddPart(root, PrimitiveType.Sphere, "Head", new Vector3(0f, 0.22f, 1.15f),
                Quaternion.identity, new Vector3(0.55f, 0.38f, 0.7f));
            AddPart(root, PrimitiveType.Sphere, "Tail", new Vector3(0f, 0.14f, -1.2f),
                Quaternion.identity, new Vector3(0.35f, 0.22f, 0.7f));
            AddPart(root, PrimitiveType.Sphere, "EyeL", new Vector3(-0.16f, 0.4f, 1.22f),
                Quaternion.identity, Vector3.one * 0.12f);
            AddPart(root, PrimitiveType.Sphere, "EyeR", new Vector3(0.16f, 0.4f, 1.22f),
                Quaternion.identity, Vector3.one * 0.12f);
        }

        private static void AddPart(
            Transform parent,
            PrimitiveType type,
            string name,
            Vector3 localPos,
            Quaternion localRot,
            Vector3 localScale)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            go.transform.localScale = localScale;

            var col = go.GetComponent<Collider>();
            if (col != null)
            {
                if (Application.isPlaying)
                    Object.Destroy(col);
                else
                    Object.DestroyImmediate(col);
            }

            var rend = go.GetComponent<Renderer>();
            if (rend != null)
            {
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                rend.receiveShadows = false;
            }
        }

        private void Apply()
        {
            EnsureShape();
            if (_mat == null)
                _mat = Bayou.Rendering.BayouShaderUtil.CreateUnlitColor(color);

            if (_parts == null) return;
            for (var i = 0; i < _parts.Length; i++)
            {
                var rend = _parts[i];
                if (rend == null) continue;
                rend.enabled = true;
                rend.sharedMaterial = _mat;
            }

            ApplyTint(color);
        }

        private void ApplyTint(Color c)
        {
            if (_mat == null) Apply();
            if (_mat == null) return;
            Bayou.Rendering.BayouShaderUtil.ApplyColor(_mat, c);
        }
    }
}
