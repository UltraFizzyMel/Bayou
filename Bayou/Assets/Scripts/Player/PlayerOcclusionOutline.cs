using System.Collections.Generic;
using Bayou.Environment;
using Bayou.Fish;
using Bayou.Fishing;
using Bayou.Quests;
using UnityEngine;
using UnityEngine.Rendering;

namespace Bayou.Player
{
    /// <summary>
    /// Glowing silhouette when the camera cannot see the body. Uses physics (including
    /// building trigger volumes) plus mesh bounds, because many buildings have no solid collider.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(80)]
    public sealed class PlayerOcclusionOutline : MonoBehaviour
    {
        [SerializeField] private Color outlineColor = new(0.45f, 1f, 0.78f, 1f);
        [SerializeField] [Range(0f, 1f)] private float hiddenFillAlpha = 0.4f;
        [SerializeField] private float chestHeight = 1.05f;
        [SerializeField] private LayerMask occlusionMask = ~0;
        [SerializeField] private float checkInterval = 0.04f;
        [SerializeField] [Range(1, 5)] private int minBlockedSamples = 3;

        private static readonly RaycastHit[] Hits = new RaycastHit[32];
        private readonly Vector3[] _samples = new Vector3[5];
        private readonly List<Renderer> _meshOccluders = new(128);

        private Material _outlineMat;
        private Renderer[] _outlineRenderers = System.Array.Empty<Renderer>();
        private Camera _cam;
        private float _nextCheck;
        private float _nextMeshRefresh;
        private bool _visible;
        private int _occludedStreak;
        private int _clearStreak;

        public static void EnsureOn(GameObject player)
        {
            if (player == null) return;
            var existing = player.GetComponent<PlayerOcclusionOutline>();
            if (existing != null)
                existing.enabled = false;
        }

        private void Awake()
        {
            enabled = false;
        }

        private void OnDestroy()
        {
            if (_outlineMat != null)
                Destroy(_outlineMat);
        }

        private void LateUpdate()
        {
        }

        private bool IsOccluded()
        {
            var cam = ResolveCamera();
            if (cam == null) return false;

            RefreshMeshOccludersIfNeeded();

            var origin = cam.transform.position;
            var root = transform.position;
            var forward = cam.transform.forward;
            var right = Vector3.Cross(Vector3.up, forward);
            if (right.sqrMagnitude < 0.001f)
                right = Vector3.right;
            right.Normalize();

            _samples[0] = root + Vector3.up * (chestHeight + 0.5f);
            _samples[1] = root + Vector3.up * chestHeight;
            _samples[2] = root + Vector3.up * 0.5f;
            _samples[3] = root + Vector3.up * chestHeight + right * 0.32f;
            _samples[4] = root + Vector3.up * chestHeight - right * 0.32f;

            var blocked = 0;
            for (var i = 0; i < _samples.Length; i++)
            {
                if (LineOccluded(origin, _samples[i]))
                    blocked++;
            }

            return blocked >= minBlockedSamples;
        }

        private bool LineOccluded(Vector3 from, Vector3 to)
        {
            var delta = to - from;
            var dist = delta.magnitude;
            if (dist < 0.05f) return false;
            var dir = delta / dist;

            if (PhysicsBlocked(from, dir, dist))
                return true;
            return BuildingMeshBlocked(from, dir, dist);
        }

        private bool BuildingMeshBlocked(Vector3 from, Vector3 dir, float dist)
        {
            var ray = new Ray(from, dir);
            var chest = transform.position + Vector3.up * chestHeight;
            for (var i = 0; i < _meshOccluders.Count; i++)
            {
                var r = _meshOccluders[i];
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy)
                    continue;

                var b = r.bounds;
                if (b.size.y < 2f)
                    continue;
                if (b.Contains(chest) || b.Contains(transform.position))
                    continue;
                if (!b.IntersectRay(ray, out var hitDist))
                    continue;
                if (hitDist < 1.2f || hitDist >= dist - 0.35f)
                    continue;

                var along = Vector3.Dot(b.center - from, dir);
                if (along < 1.2f || along > dist - 0.6f)
                    continue;

                return true;
            }

            return false;
        }

        private bool PhysicsBlocked(Vector3 from, Vector3 dir, float dist)
        {
            var count = Physics.RaycastNonAlloc(from, dir, Hits, dist - 0.08f, occlusionMask,
                QueryTriggerInteraction.Collide);

            var bestDist = float.MaxValue;
            Collider best = null;
            var bestNormal = Vector3.up;
            var bestPoint = Vector3.zero;

            for (var i = 0; i < count; i++)
            {
                var hit = Hits[i];
                if (hit.collider == null || hit.distance >= bestDist)
                    continue;
                if (ShouldIgnoreCollider(hit.collider, hit.normal, hit.point))
                    continue;
                if (!IsSubstantialOccluder(hit.collider, hit.point))
                    continue;

                bestDist = hit.distance;
                best = hit.collider;
                bestNormal = hit.normal;
                bestPoint = hit.point;
            }

            if (best == null)
                return false;

            var feetY = transform.position.y;
            if (bestNormal.y > 0.4f && bestPoint.y < feetY + 1.35f)
                return false;

            return true;
        }

        private bool ShouldIgnoreCollider(Collider col, Vector3 normal, Vector3 point)
        {
            if (col.transform == transform || col.transform.IsChildOf(transform))
                return true;
            if (IsWaterCollider(col))
                return true;
            if (col is TerrainCollider || col.GetComponent<Terrain>() != null)
                return true;
            if (col.GetComponentInParent<PondShinyCollectible>() != null)
                return true;
            if (col.GetComponentInParent<QuestItemPickup>() != null)
                return true;
            if (col.GetComponentInParent<NetScoopLoot>() != null)
                return true;
            if (col.GetComponentInParent<BayouFish>() != null)
                return true;

            var n = col.gameObject.name;
            var root = col.transform.root != null ? col.transform.root.name : n;
            if (ContainsIgnoreToken(n) || ContainsIgnoreToken(root))
                return true;

            var feetY = transform.position.y;
            // Shore / ground lip between the isometric camera and the feet.
            var flat = point - transform.position;
            flat.y = 0f;
            if (point.y < feetY + 1.2f && (normal.y > 0.35f || flat.sqrMagnitude < 2.6f * 2.6f))
                return true;

            return false;
        }

        private bool IsSubstantialOccluder(Collider col, Vector3 point)
        {
            var n = col.gameObject.name;
            var root = col.transform.root != null ? col.transform.root.name : n;
            var namedBuilding = IsBuildingToken(n) || IsBuildingToken(root);

            if (col.isTrigger && !namedBuilding)
                return false;

            // Huge area volumes (Church Area) that the player is standing inside.
            if (col.isTrigger && col.bounds.Contains(transform.position + Vector3.up * chestHeight))
                return false;

            var height = col.bounds.size.y;
            if (height < 1.4f && !namedBuilding)
                return false;

            // Nearby low hits are banks and steps, not walls.
            if (point.y < transform.position.y + 1.15f && height < 2.2f && !namedBuilding)
                return false;

            return true;
        }

        private static bool IsWaterCollider(Collider col)
        {
            if (col == null) return false;
            if (col.gameObject.layer == 4)
                return true;
            if (col.CompareTag("Water"))
                return true;
            if (col.GetComponent<WaterVolume>() != null)
                return true;
            if (col.GetComponentInParent<WaterVolume>() != null)
                return true;
            var n = col.gameObject.name;
            return n.IndexOf("water", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void RefreshMeshOccludersIfNeeded()
        {
            if (Time.unscaledTime < _nextMeshRefresh && _meshOccluders.Count > 0)
                return;

            _nextMeshRefresh = Time.unscaledTime + 1.25f;
            _meshOccluders.Clear();

            var renderers = FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
            for (var i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (!IsStructureMesh(r))
                    continue;
                _meshOccluders.Add(r);
            }
        }

        private bool IsStructureMesh(Renderer r)
        {
            if (r == null || r is ParticleSystemRenderer || r is LineRenderer)
                return false;
            if (r.transform == transform || r.transform.IsChildOf(transform))
                return false;
            if (r.name.Contains("OcclusionOutline"))
                return false;

            var n = r.gameObject.name;
            var root = r.transform.root != null ? r.transform.root.name : n;
            if (ContainsIgnoreToken(n) || ContainsIgnoreToken(root))
                return false;

            // Mesh fallback is only for real buildings (churches are often trigger volumes).
            return IsBuildingToken(n) || IsBuildingToken(root);
        }

        private static bool IsBuildingToken(string n)
        {
            if (string.IsNullOrEmpty(n)) return false;
            return HasToken(n, "church") ||
                   HasToken(n, "tomb") ||
                   HasToken(n, "mausol") ||
                   HasToken(n, "building") ||
                   HasToken(n, "house") ||
                   HasToken(n, "cabin") ||
                   HasToken(n, "shack") ||
                   HasToken(n, "wall") ||
                   HasToken(n, "gate");
        }

        private static bool ContainsIgnoreToken(string n)
        {
            if (string.IsNullOrEmpty(n)) return false;
            return HasToken(n, "tree") ||
                   HasToken(n, "palm") ||
                   HasToken(n, "lily") ||
                   HasToken(n, "grass") ||
                   HasToken(n, "water") ||
                   HasToken(n, "pond") ||
                   HasToken(n, "bush") ||
                   HasToken(n, "fern") ||
                   HasToken(n, "lamp") ||
                   HasToken(n, "light") ||
                   HasToken(n, "lantern") ||
                   HasToken(n, "torch") ||
                   HasToken(n, "fog") ||
                   HasToken(n, "ground") ||
                   HasToken(n, "terrain") ||
                   HasToken(n, "floor") ||
                   HasToken(n, "plane");
        }

        private static bool HasToken(string n, string token) =>
            n.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) >= 0;

        private void SetOutlineVisible(bool on)
        {
            _visible = on;
            for (var i = 0; i < _outlineRenderers.Length; i++)
            {
                if (_outlineRenderers[i] != null)
                    _outlineRenderers[i].enabled = on;
            }
        }

        private Camera ResolveCamera()
        {
            if (_cam != null) return _cam;
            _cam = Camera.main;
            if (_cam == null)
            {
                var follow = FindFirstObjectByType<Bayou.CameraControl.BayouFollowCamera>();
                if (follow != null)
                    _cam = follow.GetComponent<Camera>();
            }

            if (_cam == null)
                _cam = FindFirstObjectByType<Camera>();

            return _cam;
        }

        private void BuildOutlineMeshes()
        {
            var sources = GetComponentsInChildren<Renderer>(true);
            var hasCharacterMesh = false;
            for (var i = 0; i < sources.Length; i++)
            {
                if (IsCharacterMesh(sources[i]))
                {
                    hasCharacterMesh = true;
                    break;
                }
            }

            var built = new List<Renderer>(sources.Length);
            for (var i = 0; i < sources.Length; i++)
            {
                var src = sources[i];
                if (!ShouldOutline(src, hasCharacterMesh))
                    continue;

                var ghost = CreateGhost(src);
                if (ghost != null)
                    built.Add(ghost);
            }

            _outlineRenderers = built.ToArray();
        }

        private static bool IsCharacterMesh(Renderer src)
        {
            if (src is SkinnedMeshRenderer smr && smr.sharedMesh != null)
                return true;
            if (src is not MeshRenderer)
                return false;
            var mf = src.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null)
                return false;
            if (mf.sharedMesh.name == "Capsule")
                return false;
            if (src.GetComponent<CapsuleCollider>() != null && src.GetComponent<BayouCharacterMotor>() != null)
                return false;
            return true;
        }

        private static bool ShouldOutline(Renderer src, bool hasCharacterMesh)
        {
            if (src == null) return false;
            if (src is ParticleSystemRenderer || src is LineRenderer)
                return false;
            if (src.name.Contains("OcclusionOutline"))
                return false;
            if (IsHeldProp(src.transform))
                return false;

            if (src is SkinnedMeshRenderer smr)
                return smr.sharedMesh != null;

            if (src is MeshRenderer)
            {
                var mf = src.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null)
                    return false;

                var isPhysicsCapsule =
                    mf.sharedMesh.name == "Capsule" ||
                    (src.GetComponent<CapsuleCollider>() != null &&
                     src.GetComponent<BayouCharacterMotor>() != null);

                if (isPhysicsCapsule && hasCharacterMesh)
                    return false;

                return true;
            }

            return false;
        }

        private Renderer CreateGhost(Renderer src)
        {
            var go = new GameObject($"{src.name}_OcclusionOutline");
            go.transform.SetParent(src.transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            go.layer = src.gameObject.layer;

            Renderer copy;
            if (src is SkinnedMeshRenderer smr && smr.sharedMesh != null)
            {
                var ghost = go.AddComponent<SkinnedMeshRenderer>();
                ghost.sharedMesh = smr.sharedMesh;
                ghost.rootBone = smr.rootBone;
                ghost.bones = smr.bones;
                ghost.quality = smr.quality;
                ghost.updateWhenOffscreen = true;
                copy = ghost;
            }
            else
            {
                var mf = src.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null)
                {
                    Destroy(go);
                    return null;
                }

                go.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                copy = go.AddComponent<MeshRenderer>();
            }

            copy.sharedMaterial = _outlineMat;
            copy.shadowCastingMode = ShadowCastingMode.Off;
            copy.receiveShadows = false;
            copy.lightProbeUsage = LightProbeUsage.Off;
            copy.reflectionProbeUsage = ReflectionProbeUsage.Off;
            copy.allowOcclusionWhenDynamic = false;
            copy.enabled = false;
            return copy;
        }

        private static bool IsHeldProp(Transform t)
        {
            while (t != null)
            {
                var n = t.name;
                if (n.StartsWith("HeldRod") || n.StartsWith("HeldNet") || n.StartsWith("HeldLantern"))
                    return true;
                if (n.Contains("OcclusionOutline"))
                    return true;
                t = t.parent;
            }

            return false;
        }

        private Material CreateOutlineMaterial()
        {
            var shader = Shader.Find("Bayou/AlwaysVisibleUnlit") ??
                         Resources.Load<Shader>("Bayou/AlwaysVisibleUnlit");
            Material mat;
            if (shader != null)
            {
                mat = new Material(shader);
                mat.SetColor("_Color", outlineColor);
                mat.SetFloat("_FillAlpha", hiddenFillAlpha);
                mat.renderQueue = 4000;
                return mat;
            }

            mat = Bayou.Rendering.BayouShaderUtil.CreateUnlitColor(outlineColor);
            mat.renderQueue = 4000;
            return mat;
        }
    }
}
