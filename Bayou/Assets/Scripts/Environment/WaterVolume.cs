using UnityEngine;

namespace Bayou.Environment
{
    public enum WaterDepthLevel
    {
        None = 0,
        Wade = 1,
        Swim = 2
    }

    /// <summary>
    /// Marks a collider as water. Colliders are forced to triggers so the player can walk in
    /// without snagging on vertical mesh edges. Wade vs swim is either authored on this volume
    /// or inferred from how far the player is under the surface.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WaterVolume : MonoBehaviour
    {
        public const string DeepChildName = "DeepWater";

        [Tooltip("Optional: require this GameObject's tag. Leave empty to accept any.")]
        [SerializeField] private string requiredTag = "Water";

        [Header("Depth")]
        [SerializeField] private WaterDepthLevel depthLevel = WaterDepthLevel.Wade;
        [Tooltip("Submersion (surface Y minus feet Y) that counts as wading.")]
        [SerializeField] private float wadeSubmersion = 0.12f;
        [Tooltip("Submersion at or above this value counts as swimming.")]
        [SerializeField] private float swimSubmersion = 0.85f;
        [SerializeField] private bool spawnInnerSwimZone = false;
        [SerializeField] [Range(0.15f, 0.45f)] private float innerSwimInset = 0.28f;

        [Header("Trigger volume")]
        [Tooltip("How far the trigger extends below the water surface.")]
        [SerializeField] private float triggerDepth = 2.6f;
        [Tooltip("How far the trigger extends above the water surface.")]
        [SerializeField] private float triggerAboveSurface = 0.08f;

        private Collider _trigger;
        private bool _prepared;

        public WaterDepthLevel DepthLevel => depthLevel;
        public float WadeSubmersion => wadeSubmersion;
        public float SwimSubmersion => swimSubmersion;
        public Collider TriggerCollider => _trigger != null ? _trigger : GetComponent<Collider>();

        public float SurfaceY
        {
            get
            {
                var col = TriggerCollider;
                if (col != null)
                    return col.bounds.max.y - triggerAboveSurface * 0.35f;
                var rend = GetComponent<Renderer>();
                if (rend != null)
                    return rend.bounds.max.y;
                return transform.position.y;
            }
        }

        public bool Matches(GameObject other)
        {
            if (string.IsNullOrWhiteSpace(requiredTag)) return true;
            return other != null && other.CompareTag(requiredTag);
        }

        public WaterDepthLevel Evaluate(Vector3 playerFeet)
        {
            var surface = SurfaceY;
            var submersion = surface - playerFeet.y;
            var hasGround = TryGetGroundY(playerFeet, out var ground);
            var column = hasGround ? surface - ground : submersion;

            // Bank / dry ground: terrain at or above the waterline.
            if (hasGround && ground >= surface - 0.04f)
                return WaterDepthLevel.None;

            if (column < wadeSubmersion && submersion < wadeSubmersion)
                return WaterDepthLevel.None;

            // Swim only when the water column is actually deep — never because
            // of an inner XZ zone. Shallow ponds stay wade even in the middle.
            if (column >= swimSubmersion)
                return WaterDepthLevel.Swim;

            return WaterDepthLevel.Wade;
        }

        private static bool TryGetGroundY(Vector3 feet, out float y)
        {
            var terrains = Terrain.activeTerrains;
            for (var i = 0; i < terrains.Length; i++)
            {
                var terrain = terrains[i];
                if (terrain == null || terrain.terrainData == null) continue;
                var local = feet - terrain.transform.position;
                var size = terrain.terrainData.size;
                if (local.x < 0f || local.z < 0f || local.x > size.x || local.z > size.z)
                    continue;
                y = terrain.SampleHeight(feet) + terrain.transform.position.y;
                return true;
            }

            var origin = feet + Vector3.up * 0.6f;
            if (Physics.Raycast(origin, Vector3.down, out var hit, 4f, ~0, QueryTriggerInteraction.Ignore) &&
                hit.collider != null &&
                hit.collider.GetComponentInParent<WaterVolume>() == null &&
                !hit.collider.CompareTag("Water"))
            {
                y = hit.point.y;
                return true;
            }

            y = 0f;
            return false;
        }

        private void Reset()
        {
            EnsureTriggerVolume();
        }

        private void Awake()
        {
            EnsureTriggerVolume();
            DestroyInnerSwimZone();
        }

        private void OnEnable()
        {
            EnsureTriggerVolume();
        }

        public void EnsureTriggerVolume()
        {
            DestroySupportFloor();
            if (_prepared && _trigger != null && _trigger.isTrigger)
                return;

            // Mesh colliders on water planes are non-convex, so they cannot be triggers.
            // They also make the player rock/jitter, so they stay disabled.
            var meshes = GetComponents<MeshCollider>();
            for (var i = 0; i < meshes.Length; i++)
            {
                if (meshes[i] == null) continue;
                meshes[i].convex = false;
                meshes[i].isTrigger = false;
                meshes[i].enabled = false;
            }

            var localSize = LocalSurfaceSize();
            var box = GetComponent<BoxCollider>();
            if (box == null)
                box = gameObject.AddComponent<BoxCollider>();

            var above = Mathf.Clamp(triggerAboveSurface, 0.04f, 0.12f);
            var height = Mathf.Max(0.6f, triggerDepth + above);
            box.size = new Vector3(localSize.x, height, localSize.z);
            box.center = new Vector3(0f, (above - triggerDepth) * 0.5f, 0f);
            box.isTrigger = true;
            box.enabled = true;
            _trigger = box;
            _prepared = true;

            if (!string.IsNullOrWhiteSpace(requiredTag) && !gameObject.CompareTag(requiredTag))
            {
                try
                {
                    gameObject.tag = requiredTag;
                }
                catch (UnityException)
                {
                    // Tag may not exist in the project; matching still works via this component.
                }
            }
        }

        private Vector3 LocalSurfaceSize()
        {
            var meshFilter = GetComponent<MeshFilter>();
            if (meshFilter != null && meshFilter.sharedMesh != null)
            {
                var b = meshFilter.sharedMesh.bounds.size;
                return new Vector3(Mathf.Max(0.5f, b.x), 1f, Mathf.Max(0.5f, b.z));
            }

            var rend = GetComponent<Renderer>();
            if (rend != null)
            {
                var b = rend.localBounds.size;
                return new Vector3(Mathf.Max(0.5f, b.x), 1f, Mathf.Max(0.5f, b.z));
            }

            return new Vector3(10f, 1f, 10f);
        }

        private void DestroySupportFloor()
        {
            const string name = "WaterSupport";
            for (var i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (child == null || child.name != name) continue;
                if (Application.isPlaying)
                    Destroy(child.gameObject);
                else
                    DestroyImmediate(child.gameObject);
            }
        }

        private void DestroyInnerSwimZone()
        {
            var existing = transform.Find(DeepChildName);
            if (existing == null) return;
            DestroyImmediate(existing.gameObject);
        }

        private void EnsureInnerSwimZone()
        {
            var existing = transform.Find(DeepChildName);
            if (existing != null)
            {
                var vol = existing.GetComponent<WaterVolume>();
                if (vol != null)
                    vol.EnsureTriggerVolume();
                return;
            }

            var inset = Mathf.Clamp(innerSwimInset, 0.12f, 0.45f);
            var scale = 1f - inset * 2f;
            if (scale < 0.2f)
                return;

            var go = new GameObject(DeepChildName);
            go.SetActive(false);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = new Vector3(scale, 1f, scale);
            go.layer = gameObject.layer;
            if (!string.IsNullOrWhiteSpace(requiredTag))
            {
                try { go.tag = requiredTag; }
                catch (UnityException) { }
            }

            var parentBox = GetComponent<BoxCollider>();
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            if (parentBox != null)
            {
                box.size = parentBox.size;
                box.center = parentBox.center;
            }

            var deep = go.AddComponent<WaterVolume>();
            deep.requiredTag = requiredTag;
            deep.depthLevel = WaterDepthLevel.Swim;
            deep.spawnInnerSwimZone = false;
            deep.wadeSubmersion = wadeSubmersion;
            deep.swimSubmersion = 0.01f;
            deep.triggerDepth = triggerDepth;
            deep.triggerAboveSurface = triggerAboveSurface;
            deep._trigger = box;
            deep._prepared = true;
            go.SetActive(true);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            var col = TriggerCollider;
            if (col == null) return;
            Gizmos.color = depthLevel == WaterDepthLevel.Swim
                ? new Color(0.1f, 0.25f, 0.85f, 0.25f)
                : new Color(0.2f, 0.6f, 1f, 0.18f);
            Gizmos.matrix = Matrix4x4.identity;
            Gizmos.DrawCube(col.bounds.center, col.bounds.size);
            Gizmos.color = new Color(0.4f, 0.85f, 1f, 0.9f);
            Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
        }
#endif
    }
}
