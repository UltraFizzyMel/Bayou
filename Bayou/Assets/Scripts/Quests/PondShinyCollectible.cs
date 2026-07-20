using Bayou.Inventory;
using UnityEngine;

namespace Bayou.Quests
{
    /// <summary>
    /// Shiny quest item that lives in pond water and is collected with the fishing net
    /// (thrown net plant or hand-net scoop) — not by pressing E.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PondShinyCollectible : MonoBehaviour
    {
        [SerializeField] private ItemDefinition item;
        [SerializeField] private float bobAmplitude = 0.08f;
        [SerializeField] private float bobSpeed = 2.2f;
        [SerializeField] private Color glowColor = new(0.35f, 0.95f, 0.45f, 1f);

        private Vector3 _basePos;
        private bool _collected;
        private Renderer _renderer;

        public ItemDefinition Item => item;
        public bool IsCollected => _collected;

        private void Awake()
        {
            _basePos = transform.position;
            _renderer = GetComponentInChildren<Renderer>();
            ApplyGlow();
        }

        private void Update()
        {
            if (_collected) return;
            var y = _basePos.y + Mathf.Sin(Time.time * bobSpeed) * bobAmplitude;
            transform.position = new Vector3(_basePos.x, y, _basePos.z);
        }

        /// <summary>Called by thrown net / hand net. Returns true if this shiny was collected.</summary>
        public bool TryCollectFromNet(Vector3 netPos, float radius)
        {
            if (_collected || item == null) return false;

            var flat = transform.position - netPos;
            flat.y = 0f;
            if (flat.sqrMagnitude > radius * radius)
                return false;

            return Collect();
        }

        public static bool TryScoopNear(Vector3 netPos, float radius)
        {
            var all = FindObjectsByType<PondShinyCollectible>(FindObjectsSortMode.None);
            foreach (var shiny in all)
            {
                if (shiny != null && shiny.TryCollectFromNet(netPos, radius))
                    return true;
            }

            return false;
        }

        private bool Collect()
        {
            if (_collected) return false;

            var inv = InventoryController.Instance;
            if (inv == null)
            {
                Debug.LogWarning("[PondShiny] No InventoryController.");
                return false;
            }

            if (!inv.TryAddItem(item) && !inv.TryHoldNewItem(item, out _))
            {
                Debug.LogWarning($"[PondShiny] Could not add {item.displayName} — bag full?");
                return false;
            }

            _collected = true;
            Debug.Log($"[PondShiny] Scooped {item.displayName} with the net.");
            Destroy(gameObject);
            return true;
        }

        private void ApplyGlow()
        {
            if (_renderer == null) return;
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            var mat = new Material(shader);
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", glowColor);
            else if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", glowColor);
            _renderer.sharedMaterial = mat;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (item == null)
            {
                item = UnityEditor.AssetDatabase.LoadAssetAtPath<ItemDefinition>(
                    "Assets/Inventory/Items/Item_ShinyPond.asset");
            }
        }
#endif
    }
}
