using Bayou.Environment;
using UnityEngine;

namespace Bayou.Player
{
    /// <summary>
    /// Tags this character for Linework Lite's Free Outline renderer feature.
    /// Matches rendering layer index 8 ("PlayerOutline") on the outline settings asset.
    /// Outline is enabled only when a building / prop sits between camera and the body —
    /// terrain, water, and ground under the feet are ignored so the rim does not stick to the soles.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(80)]
    public sealed class PlayerOutline : MonoBehaviour
    {
        public const int RenderingLayerIndex = 8;
        public const uint RenderingLayerBit = 1u << RenderingLayerIndex;

        [SerializeField] private float chestHeight = 0.55f;
        [SerializeField] private float headHeight = 0.95f;
        [SerializeField] private float checkInterval = 0.08f;
        [SerializeField] [Range(1, 5)] private int minOccludedSamples = 1;
        [SerializeField] [Range(1, 8)] private int occludedFramesToShow = 2;
        [SerializeField] [Range(1, 12)] private int clearFramesToHide = 4;

        private Renderer[] _targets = System.Array.Empty<Renderer>();
        private readonly RaycastHit[] _hits = new RaycastHit[12];
        private float _nextCheck;
        private int _occludedStreak;
        private int _clearStreak;
        private bool _outlined;

        public static void EnsureOn(GameObject player)
        {
            if (player == null) return;
            var outline = player.GetComponent<PlayerOutline>();
            if (outline == null)
                outline = player.AddComponent<PlayerOutline>();
            outline.enabled = true;
        }

        private void OnEnable()
        {
            CollectTargets();
            SetOutlined(false);
        }

        private void Start()
        {
            CollectTargets();
            SetOutlined(false);
        }

        private void OnDisable()
        {
            SetOutlined(false);
        }

        private void LateUpdate()
        {
            if (Time.time < _nextCheck) return;
            _nextCheck = Time.time + checkInterval;

            if (_targets.Length == 0)
                CollectTargets();

            var occluded = IsOccludedByCover();
            if (occluded)
            {
                _occludedStreak++;
                _clearStreak = 0;
                if (!_outlined && _occludedStreak >= occludedFramesToShow)
                    SetOutlined(true);
            }
            else
            {
                _clearStreak++;
                _occludedStreak = 0;
                if (_outlined && _clearStreak >= clearFramesToHide)
                    SetOutlined(false);
            }
        }

        private void CollectTargets()
        {
            var hasCharacterMesh = HasCharacterMesh();
            var renderers = GetComponentsInChildren<Renderer>(true);
            var count = 0;
            for (var i = 0; i < renderers.Length; i++)
            {
                if (ShouldOutline(renderers[i], hasCharacterMesh))
                    count++;
            }

            _targets = new Renderer[count];
            var n = 0;
            for (var i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (!ShouldOutline(r, hasCharacterMesh)) continue;
                _targets[n++] = r;
            }
        }

        private bool HasCharacterMesh()
        {
            var skinned = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (var i = 0; i < skinned.Length; i++)
            {
                if (skinned[i] != null && skinned[i].sharedMesh != null)
                    return true;
            }

            return false;
        }

        private bool ShouldOutline(Renderer renderer, bool hasCharacterMesh)
        {
            if (renderer == null) return false;
            if (renderer is ParticleSystemRenderer or LineRenderer or TrailRenderer or SpriteRenderer)
                return false;
            if (IsHeldProp(renderer.transform))
                return false;
            if (hasCharacterMesh && renderer.gameObject == gameObject)
                return false;
            return true;
        }

        private void SetOutlined(bool enable)
        {
            _outlined = enable;
            for (var i = 0; i < _targets.Length; i++)
            {
                var r = _targets[i];
                if (r != null)
                    SetBit(r, enable);
            }
        }

        private bool IsOccludedByCover()
        {
            var cam = Camera.main;
            if (cam == null) return false;

            var origin = cam.transform.position;
            var blocked = 0;
            if (HitsCover(origin, transform.position + Vector3.up * chestHeight))
                blocked++;
            if (HitsCover(origin, transform.position + Vector3.up * headHeight))
                blocked++;
            if (HitsCover(origin, transform.position + transform.forward * 0.15f + Vector3.up * chestHeight))
                blocked++;

            return blocked >= minOccludedSamples;
        }

        private bool HitsCover(Vector3 origin, Vector3 target)
        {
            var to = target - origin;
            var dist = to.magnitude;
            if (dist < 0.05f) return false;

            var count = Physics.RaycastNonAlloc(
                origin, to / dist, _hits, dist - 0.08f, ~0, QueryTriggerInteraction.Ignore);

            var bestDist = float.PositiveInfinity;
            var foundCover = false;
            for (var i = 0; i < count; i++)
            {
                var hit = _hits[i];
                if (hit.collider == null) continue;
                if (hit.distance >= bestDist) continue;
                if (IsSelf(hit.collider.transform)) continue;
                if (IsIgnoredOccluder(hit.collider, hit.point, hit.normal)) continue;
                bestDist = hit.distance;
                foundCover = true;
            }

            return foundCover;
        }

        private bool IsSelf(Transform t)
        {
            return t != null && t.IsChildOf(transform);
        }

        private bool IsIgnoredOccluder(Collider col, Vector3 point, Vector3 normal)
        {
            if (col is TerrainCollider)
                return true;
            if (col.GetComponent<Terrain>() != null || col.GetComponentInParent<Terrain>() != null)
                return true;
            if (col.GetComponent<WaterVolume>() != null || col.GetComponentInParent<WaterVolume>() != null)
                return true;
            if (col.CompareTag("Water"))
                return true;

            var n = col.name;
            if (n.IndexOf("Lamp", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (n.IndexOf("Water", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (n.IndexOf("Terrain", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (n.IndexOf("Ground", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            var feetY = transform.position.y - 1.05f;
            if (point.y < feetY + 0.28f)
                return true;
            if (normal.y > 0.65f && point.y < transform.position.y)
                return true;

            return false;
        }

        private static void SetBit(Renderer renderer, bool enable)
        {
            var mask = (uint)renderer.renderingLayerMask;
            if (enable) mask |= RenderingLayerBit;
            else mask &= ~RenderingLayerBit;
            renderer.renderingLayerMask = mask;
        }

        private static bool IsHeldProp(Transform t)
        {
            while (t != null)
            {
                var n = t.name;
                if (n.StartsWith("HeldRod") || n.StartsWith("HeldNet") || n.StartsWith("HeldLantern"))
                    return true;
                t = t.parent;
            }

            return false;
        }
    }
}
