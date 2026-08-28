using Bayou.Inventory;
using Bayou.Player;
using Bayou.UI;
using UnityEngine;

namespace Bayou.Quests
{
    /// <summary>
    /// Shiny quest item in pond water. Collect with the fishing net (thrown or hand scoop)
    /// or by standing on it and pressing Interact — both open the fish-style confirm UI.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PondShinyCollectible : MonoBehaviour, IInteractionPromptSource
    {
        private const string ResourcesItemPath = "Bayou/Items/Item_ShinyPond";

        [SerializeField] private ItemDefinition item;
        [SerializeField] private float bobAmplitude = 0.08f;
        [SerializeField] private float bobSpeed = 2.2f;
        [SerializeField] private Color glowColor = new(0.35f, 0.95f, 0.45f, 1f);
        [SerializeField] private float extraCollectRadius = 3.5f;
        [SerializeField] private string pickupPrompt = "Pick up";

        private Vector3 _basePos;
        private bool _collected;
        private bool _playerInRange;
        private Renderer _renderer;

        public ItemDefinition Item => item;
        public bool IsCollected => _collected;

        private void Awake()
        {
            _basePos = transform.position;
            _renderer = GetComponentInChildren<Renderer>();
            ResolveItem();
            ApplyGlow();
        }

        private void OnEnable() => InteractionPromptBroker.Register(this);
        private void OnDisable() => InteractionPromptBroker.Unregister(this);

        private void Update()
        {
            if (_collected) return;

            var y = _basePos.y + Mathf.Sin(Time.time * bobSpeed) * bobAmplitude;
            transform.position = new Vector3(_basePos.x, y, _basePos.z);

            if (!_playerInRange)
                return;

            var dialogue = DialogueManager.GetInstance();
            if (dialogue != null && dialogue.dialogueIsPlaying)
                return;

            var input = InputManager.GetInstance();
            if (input != null && input.GetInteractPressed())
                Collect();
        }

        /// <summary>Called by thrown net / hand net. Returns true if this shiny was collected.</summary>
        public bool TryCollectFromNet(Vector3 netPos, float radius)
        {
            if (_collected) return false;
            ResolveItem();
            if (item == null) return false;

            var reach = Mathf.Max(radius, extraCollectRadius);
            var col = GetComponent<Collider>();
            if (col != null)
                reach += WorldColliderRadius(col);

            var flat = transform.position - netPos;
            flat.y = 0f;
            if (flat.sqrMagnitude > reach * reach)
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

        public bool TryGetInteractionPrompt(out InteractionPrompt prompt)
        {
            prompt = default;
            if (!_playerInRange || _collected || item == null)
                return false;

            var name = string.IsNullOrWhiteSpace(item.displayName) ? item.name : item.displayName;
            var action = string.IsNullOrWhiteSpace(pickupPrompt) ? $"Pick up {name}" : $"{pickupPrompt} {name}";
            prompt = new InteractionPrompt("E", action.Trim(), 70, DistToPlayerSq());
            return true;
        }

        private bool Collect()
        {
            ResolveItem();
            if (_collected || item == null)
            {
                if (item == null)
                    Debug.LogError("[PondShiny] Cannot collect — Item_ShinyPond is missing.");
                return false;
            }

            _collected = true;
            Debug.Log($"[PondShiny] Collected {item.displayName}.");
            CaughtFishPresenter.Present(item);
            Destroy(gameObject);
            return true;
        }

        private void ResolveItem()
        {
            if (item != null) return;
            item = Resources.Load<ItemDefinition>(ResourcesItemPath);
        }

        private void ApplyGlow()
        {
            if (_renderer == null) return;
            _renderer.sharedMaterial = Bayou.Rendering.BayouShaderUtil.CreateUnlitColor(glowColor);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (IsPlayer(other))
                _playerInRange = true;
        }

        private void OnTriggerExit(Collider other)
        {
            if (IsPlayer(other))
                _playerInRange = false;
        }

        private static bool IsPlayer(Collider other)
        {
            return other.CompareTag("Player") ||
                   other.TryGetComponent<BayouCharacterMotor>(out _) ||
                   other.GetComponentInParent<BayouCharacterMotor>() != null;
        }

        private float DistToPlayerSq()
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p == null) return 0f;
            var d = transform.position - p.transform.position;
            d.y = 0f;
            return d.sqrMagnitude;
        }

        private static float WorldColliderRadius(Collider col)
        {
            if (col is SphereCollider sphere)
            {
                var s = sphere.transform.lossyScale;
                return sphere.radius * Mathf.Max(s.x, Mathf.Max(s.y, s.z));
            }

            var e = col.bounds.extents;
            return Mathf.Max(e.x, e.z);
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
