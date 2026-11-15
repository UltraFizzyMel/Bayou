using System.Collections.Generic;
using Bayou.Environment;
using Bayou.Player;
using UnityEngine;

namespace Bayou.Creatures
{
    /// <summary>
    /// A web shot that lands as a patch. The player is slightly slower on it.
    /// The spider that spun it moves faster while standing on its own silk.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SpiderWeb : MonoBehaviour
    {
        public const float PlayerSlow = 0.68f;
        public const float SpiderBoost = 1.7f;

        private static readonly List<SpiderWeb> All = new();

        [SerializeField] private float flightSeconds = 0.85f;
        [SerializeField] private float lifeSeconds = 8f;
        [SerializeField] private float deployRadius = 1.25f;

        private const string WebPrefabPath = "Assets/Prefabs/Webs & Cocoons/Webs/Web1.prefab";

        private static GameObject _webPrefab;
        private static WaterVolume[] _waters = System.Array.Empty<WaterVolume>();
        private static float _watersUntil;

        private SpiderAgent _owner;
        private Vector3 _target;
        private float _speed;
        private float _flightLeft;
        private float _lifeLeft;
        private bool _deployed;
        private Transform _visual;

        public SpiderAgent Owner => _owner;
        public bool IsDeployed => _deployed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => All.Clear();

        public static int CountFor(SpiderAgent owner)
        {
            var n = 0;
            for (var i = 0; i < All.Count; i++)
            {
                var web = All[i];
                if (web != null && web._owner == owner)
                    n++;
            }

            return n;
        }

        public static float PlayerSpeedMultiplier
        {
            get
            {
                var player = PlayerLocator.Transform;
                if (player == null) return 1f;
                return Covers(player.position, null) ? PlayerSlow : 1f;
            }
        }

        public static float SpeedBonusFor(SpiderAgent spider)
        {
            if (spider == null) return 1f;
            return Covers(spider.transform.position, spider) ? SpiderBoost : 1f;
        }

        /// <summary>Inside a water volume, or below that volume's surface.</summary>
        public static bool IsUnderwater(Vector3 world)
        {
            var volumes = Waters();
            for (var i = 0; i < volumes.Length; i++)
            {
                var volume = volumes[i];
                if (volume == null) continue;
                var col = volume.TriggerCollider;
                if (col == null) continue;
                var bounds = col.bounds;
                var insideXZ = world.x >= bounds.min.x && world.x <= bounds.max.x &&
                               world.z >= bounds.min.z && world.z <= bounds.max.z;
                if (!insideXZ) continue;
                if (bounds.Contains(world)) return true;
                if (world.y < volume.SurfaceY - 0.02f) return true;
            }

            return false;
        }

        public void Launch(SpiderAgent owner, Vector3 from, Vector3 target, float speed)
        {
            _owner = owner;
            _target = target;
            _target.y = from.y;
            if (IsUnderwater(_target) || IsUnderwater(from))
            {
                enabled = false;
                Destroy(gameObject);
                return;
            }

            _speed = Mathf.Max(1f, speed);
            var travel = Vector3.Distance(from, _target);
            _flightLeft = Mathf.Clamp(travel / _speed, 0.2f, flightSeconds);
            transform.position = from;
            AttachVisual();
        }

        private void OnEnable()
        {
            if (!All.Contains(this))
                All.Add(this);
        }

        private void OnDisable() => All.Remove(this);

        private void Awake()
        {
            var col = GetComponent<Collider>();
            if (col != null)
                col.isTrigger = true;

            var rend = GetComponent<Renderer>();
            if (rend != null)
                rend.sharedMaterial = Bayou.Rendering.BayouShaderUtil.CreateUnlitColor(new Color(0.82f, 0.84f, 0.78f, 1f));
        }

        private void Update()
        {
            if (!_deployed)
            {
                var step = _speed * Time.deltaTime;
                transform.position = Vector3.MoveTowards(transform.position, _target, step);
                _flightLeft -= Time.deltaTime;
                var arrived = (transform.position - _target).sqrMagnitude < 0.04f || _flightLeft <= 0f;
                if (arrived)
                    Deploy();
                return;
            }

            _lifeLeft -= Time.deltaTime;
            if (_lifeLeft <= 0f)
                Destroy(gameObject);
        }

        private void Deploy()
        {
            if (IsUnderwater(transform.position) || IsUnderwater(_target))
            {
                Destroy(gameObject);
                return;
            }

            _deployed = true;
            _lifeLeft = lifeSeconds;
            CreatureController.SnapBodyToGround(transform, 0.06f);
            FitVisual();
        }

        private void AttachVisual()
        {
            var prefab = LoadWebPrefab();
            if (prefab == null)
            {
                Debug.LogWarning("[SpiderWeb] Missing web prefab at " + WebPrefabPath);
                return;
            }

            var visual = Instantiate(prefab, transform);
            visual.name = "Web";
            _visual = visual.transform;
            _visual.localPosition = Vector3.zero;
            _visual.localRotation = Quaternion.identity;
            _visual.localScale = Vector3.one * 0.2f;
            foreach (var col in visual.GetComponentsInChildren<Collider>(true))
            {
                if (col != null)
                    col.enabled = false;
            }
        }

        private void FitVisual()
        {
            if (_visual == null) return;
            _visual.localRotation = Quaternion.identity;
            _visual.localScale = Vector3.one;
            var rend = _visual.GetComponentInChildren<Renderer>();
            if (rend == null) return;

            var size = rend.bounds.size;
            if (size.y > size.x * 1.35f && size.y > size.z * 1.35f)
                _visual.localRotation = Quaternion.Euler(90f, 0f, 0f);

            size = rend.bounds.size;
            var diameter = Mathf.Max(size.x, size.z);
            if (diameter < 0.01f) return;
            var scale = (deployRadius * 2f) / diameter;
            _visual.localScale = Vector3.one * scale;
        }

        private static GameObject LoadWebPrefab()
        {
            if (_webPrefab != null) return _webPrefab;
#if UNITY_EDITOR
            _webPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(WebPrefabPath);
#endif
            if (_webPrefab == null)
                _webPrefab = Resources.Load<GameObject>("Bayou/Webs/Web1");
            return _webPrefab;
        }

        private static WaterVolume[] Waters()
        {
            if (Time.unscaledTime < _watersUntil && _waters != null)
                return _waters;
            _watersUntil = Time.unscaledTime + 1f;
            _waters = Object.FindObjectsByType<WaterVolume>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            return _waters;
        }

        private static bool Covers(Vector3 worldPos, SpiderAgent onlyOwner)
        {
            for (var i = 0; i < All.Count; i++)
            {
                var web = All[i];
                if (web == null || !web._deployed) continue;
                if (onlyOwner != null && web._owner != onlyOwner) continue;
                var flat = worldPos - web.transform.position;
                flat.y = 0f;
                if (flat.sqrMagnitude <= web.deployRadius * web.deployRadius)
                    return true;
            }

            return false;
        }
    }
}
