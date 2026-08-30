using Bayou.Inventory;
using Bayou.Player;
using Bayou.UI;
using UnityEngine;

namespace Bayou.Quests
{
    /// <summary>
    /// Rosary in the church pond. Scoop with the hand net (or a planted rod bobber)
    /// or stand next to it and press Interact.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PondShinyCollectible : MonoBehaviour, IInteractionPromptSource
    {
        private const string ResourcesItemPath = "Bayou/Items/Item_RosaryNecklace";
        private const string CanonicalItemId = "Item_RosaryNecklace";

        [SerializeField] private ItemDefinition item;
        [SerializeField] private float bobAmplitude = 0.08f;
        [SerializeField] private float bobSpeed = 2.2f;
        [SerializeField] private Color glowColor = new(0.95f, 0.85f, 0.35f, 1f);
        [SerializeField] private float extraCollectRadius = 0.85f;
        [SerializeField] private string pickupPrompt = "Pick up";
        [SerializeField] private float interactReach = 2.2f;

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

        private void OnEnable()
        {
            InteractionPromptBroker.Register(this);
            InputManager.GetInstance()?.RegisterInteractPressed();
        }

        private void OnDisable() => InteractionPromptBroker.Unregister(this);

        private void Update()
        {
            if (_collected) return;

            var y = _basePos.y + Mathf.Sin(Time.time * bobSpeed) * bobAmplitude;
            transform.position = new Vector3(_basePos.x, y, _basePos.z);

            if (!CanCollectNow())
                return;
            if (!_playerInRange || DistToPlayerSq() > interactReach * interactReach)
                return;

            var dialogue = DialogueManager.GetInstance();
            if (dialogue != null && dialogue.dialogueIsPlaying)
                return;

            var input = InputManager.GetInstance();
            if (input != null && input.GetInteractPressed())
                Collect();
        }

        public bool TryCollectFromNet(Vector3 netPos, float radius)
        {
            if (_collected || !CanCollectNow()) return false;
            ResolveItem();
            if (item == null) return false;

            var reach = Mathf.Max(0.6f, radius) + extraCollectRadius;
            var flat = transform.position - netPos;
            flat.y = 0f;
            if (flat.sqrMagnitude > reach * reach)
                return false;

            return Collect();
        }

        private static bool CanCollectNow() => Time.timeSinceLevelLoad >= 0.45f;

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
            if (!CanCollectNow() || !_playerInRange || _collected || item == null)
                return false;
            if (DistToPlayerSq() > interactReach * interactReach)
                return false;

            var name = string.IsNullOrWhiteSpace(item.displayName) ? "rosary" : item.displayName;
            var action = string.IsNullOrWhiteSpace(pickupPrompt) ? $"Pick up {name}" : $"{pickupPrompt} {name}";
            prompt = new InteractionPrompt("E", action.Trim(), 70, DistToPlayerSq());
            return true;
        }

        private bool Collect()
        {
            if (!CanCollectNow())
                return false;

            ResolveItem();
            if (_collected || item == null)
            {
                if (item == null)
                    Debug.LogError("[PondRosary] Cannot collect — Item_RosaryNecklace is missing.");
                return false;
            }

            _collected = true;
            Debug.Log($"[PondRosary] Collected {item.displayName}.");
            CaughtFishPresenter.Present(item);
            Destroy(gameObject);
            return true;
        }

        private void ResolveItem()
        {
            if (item != null && item.MatchesId(CanonicalItemId))
                return;

            var loaded = Resources.Load<ItemDefinition>(ResourcesItemPath);
            if (loaded == null)
                loaded = Resources.Load<ItemDefinition>("Bayou/Items/Item_ShinyPond");
            if (loaded != null)
                item = loaded;
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

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (item == null || !item.MatchesId(CanonicalItemId))
            {
                item = UnityEditor.AssetDatabase.LoadAssetAtPath<ItemDefinition>(
                    "Assets/Inventory/Items/Item_RosaryNecklace.asset");
            }
        }
#endif
    }
}
