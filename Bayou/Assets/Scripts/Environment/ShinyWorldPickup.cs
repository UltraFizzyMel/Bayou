using Bayou.Inventory;
using Bayou.Player;
using Bayou.UI;
using UnityEngine;

namespace Bayou.Environment
{
    /// <summary>
    /// Sparkling world item. Interact (E) opens the place-in-bag flow
    /// (drag into the inventory, or discard). Discard puts it back on the ground.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ShinyWorldPickup : MonoBehaviour, IInteractionPromptSource
    {
        [SerializeField] private ItemDefinition item;
        [SerializeField] private string pickupPrompt = "Pick up";
        [SerializeField] private Color sparkleColor = new(1f, 0.9f, 0.45f, 1f);
        [SerializeField] private bool fallbackToCalistesBox = true;

        private bool _playerInRange;
        private bool _offered;

        public ItemDefinition Item => item;

        public void Bind(ItemDefinition definition, string prompt = null)
        {
            item = definition;
            if (!string.IsNullOrWhiteSpace(prompt))
                pickupPrompt = prompt;
            ResolveItem();
            ShinyCue.Ensure(transform, sparkleColor);
        }

        private void Reset()
        {
            var col = GetComponent<Collider>();
            if (col != null)
                col.isTrigger = true;
        }

        private void Awake()
        {
            ResolveItem();
            ShinyCue.Ensure(transform, sparkleColor);
        }

        private void OnEnable() => InteractionPromptBroker.Register(this);

        private void OnDisable() => InteractionPromptBroker.Unregister(this);

        public bool TryGetInteractionPrompt(out InteractionPrompt prompt)
        {
            prompt = default;
            if (_offered || !_playerInRange || item == null) return false;
            if (SignReadUi.IsOpen || Bayou.GameplayPause.BlocksWorldInteract || Bayou.GameplayPause.IsDialoguePlaying)
                return false;

            var name = string.IsNullOrWhiteSpace(item.displayName) ? "item" : item.displayName;
            var action = string.IsNullOrWhiteSpace(pickupPrompt) ? $"Pick up {name}" : $"{pickupPrompt} {name}";
            prompt = new InteractionPrompt("E", action.Trim(), 66, DistToPlayerSq());
            return true;
        }

        private void Update()
        {
            if (_offered)
            {
                WatchOffer();
                return;
            }

            if (!_playerInRange || item == null) return;
            if (SignReadUi.IsOpen) return;

            var input = InputManager.GetInstance();
            if (input == null || !input.GetInteractPressed())
                return;

            var inv = InventoryController.Instance ?? FindFirstObjectByType<InventoryController>();
            if (inv != null && inv.HasItemsById(item.Id, 1))
            {
                Debug.Log($"[Pickup] Already carrying {item.displayName}.");
                return;
            }

            _offered = true;
            SetShown(false);
            CaughtFishPresenter.Present(item, "Pick up");
        }

        private void WatchOffer()
        {
            if (CaughtFishPresenter.IsBusy) return;

            var inv = InventoryController.Instance ?? FindFirstObjectByType<InventoryController>();
            if (item != null && inv != null && inv.HasItemsById(item.Id, 1))
            {
                Destroy(gameObject);
                return;
            }

            _offered = false;
            SetShown(true);
            var player = PlayerLocator.Transform;
            if (player != null)
            {
                var delta = player.position - transform.position;
                delta.y = 0f;
                _playerInRange = delta.sqrMagnitude <= 2.4f * 2.4f;
            }
        }

        private void ResolveItem()
        {
            if (item != null || !fallbackToCalistesBox) return;
            item = Resources.Load<ItemDefinition>("Bayou/Items/Item_Box");
        }

        private void SetShown(bool shown)
        {
            var rend = GetComponent<Renderer>();
            if (rend != null)
                rend.enabled = shown;

            foreach (Transform child in transform)
            {
                if (child == null) continue;
                child.gameObject.SetActive(shown);
            }

            var col = GetComponent<Collider>();
            if (col != null)
                col.enabled = shown;
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

        private static bool IsPlayer(Collider other) =>
            other != null &&
            (other.CompareTag("Player") || other.GetComponentInParent<BayouCharacterMotor>() != null);

        private float DistToPlayerSq()
        {
            var p = PlayerLocator.Transform;
            if (p == null) return 0f;
            var d = transform.position - p.position;
            d.y = 0f;
            return d.sqrMagnitude;
        }
    }
}
