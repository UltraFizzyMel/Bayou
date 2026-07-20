using Bayou.Inventory;
using UnityEngine;

namespace Bayou.Fishing
{
    /// <summary>
    /// One-time water loot scooped with the hand net or a planted thrown net (rosary, etc.).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NetScoopLoot : MonoBehaviour
    {
        [SerializeField] private ItemDefinition item;
        [SerializeField] private float bobAmplitude = 0.08f;
        [SerializeField] private float bobSpeed = 2f;
        [SerializeField] private Color glowColor = new(0.9f, 0.8f, 0.25f, 1f);

        private Vector3 _basePos;
        private bool _collected;

        public void Configure(ItemDefinition definition, Color glow)
        {
            item = definition;
            glowColor = glow;
            ApplyGlow();
        }

        private void Awake()
        {
            _basePos = transform.position;
            ApplyGlow();
        }

        private void Update()
        {
            if (_collected) return;
            var y = _basePos.y + Mathf.Sin(Time.time * bobSpeed) * bobAmplitude;
            transform.position = new Vector3(_basePos.x, y, _basePos.z);
        }

        public bool TryCollectFromNet(Vector3 netPos, float radius)
        {
            if (_collected || item == null) return false;
            var flat = transform.position - netPos;
            flat.y = 0f;
            if (flat.sqrMagnitude > radius * radius) return false;
            return Collect();
        }

        public static bool TryScoopNear(Vector3 netPos, float radius)
        {
            foreach (var loot in FindObjectsByType<NetScoopLoot>(FindObjectsSortMode.None))
            {
                if (loot != null && loot.TryCollectFromNet(netPos, radius))
                    return true;
            }

            return false;
        }

        private bool Collect()
        {
            var inv = InventoryController.Instance;
            if (inv == null) return false;
            if (!inv.TryAddItem(item) && !inv.TryHoldNewItem(item, out _))
            {
                Debug.LogWarning($"[NetScoop] Bag full — could not add {item.displayName}.");
                return false;
            }

            _collected = true;
            Debug.Log($"[NetScoop] Collected {item.displayName}.");
            Destroy(gameObject);
            return true;
        }

        private void ApplyGlow()
        {
            var rend = GetComponentInChildren<Renderer>();
            if (rend == null) return;
            rend.sharedMaterial = Bayou.Rendering.BayouShaderUtil.CreateUnlitColor(glowColor);
        }
    }
}
