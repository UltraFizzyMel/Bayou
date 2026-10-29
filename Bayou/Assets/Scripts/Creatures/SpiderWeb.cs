using System.Collections.Generic;
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

        private SpiderAgent _owner;
        private Vector3 _target;
        private float _speed;
        private float _flightLeft;
        private float _lifeLeft;
        private bool _deployed;

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

        public void Launch(SpiderAgent owner, Vector3 from, Vector3 target, float speed)
        {
            _owner = owner;
            _target = target;
            _target.y = from.y;
            _speed = Mathf.Max(1f, speed);
            var travel = Vector3.Distance(from, _target);
            _flightLeft = Mathf.Clamp(travel / _speed, 0.2f, flightSeconds);
            transform.position = from;
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
            _deployed = true;
            _lifeLeft = lifeSeconds;
            CreatureController.SnapBodyToGround(transform, 0.06f);
            transform.localScale = new Vector3(deployRadius * 2f, 0.06f, deployRadius * 2f);
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
