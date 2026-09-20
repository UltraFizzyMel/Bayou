using System.Collections.Generic;
using Bayou.Demo;
using Bayou.Inventory;
using Bayou.Player;
using Bayou.UI;
using UnityEngine;

namespace Bayou.Quests
{
    /// <summary>
    /// World pickup: stand in trigger and press Interact (E) to add an item to the bag.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class QuestItemPickup : MonoBehaviour, IInteractionPromptSource
    {
        [SerializeField] private ItemDefinition item;
        [SerializeField] private string playerTag = "Player";
        [SerializeField] private bool destroyOnPickup = true;
        [SerializeField] private GameObject visualCue;
        [SerializeField] private string pickupPrompt = "Pick up";
        [Tooltip("If true, picking this up finishes the demo. Leave off for the lantern so it can be used.")]
        [SerializeField] private bool endDemoOnPickup;
        [SerializeField] private bool addStraightToBag = true;

        public ItemDefinition Item => item;
        public static IReadOnlyList<QuestItemPickup> Living => All;

        private static readonly List<QuestItemPickup> All = new();
        private bool _playerInRange;

        public void Bind(ItemDefinition definition, string prompt = "Pick up", bool addStraightToBag = false)
        {
            item = definition;
            this.addStraightToBag = addStraightToBag;
            if (!string.IsNullOrWhiteSpace(prompt))
                pickupPrompt = prompt;
            Bayou.Rendering.WorldItemVisual.EnsurePickupVisual(gameObject, item);
        }

        private void Reset()
        {
            var col = GetComponent<Collider>();
            if (col != null)
                col.isTrigger = true;
        }

        private void Awake()
        {
            Bayou.Rendering.WorldItemVisual.EnsurePickupVisual(gameObject, item);
        }

        private void Start()
        {
            if (item != null && item.IsUniqueEquipment)
                addStraightToBag = false;
            Bayou.Rendering.WorldItemVisual.EnsurePickupVisual(gameObject, item);
            Bayou.Rendering.WorldItemVisual.SnapToGround(transform, 0.22f);
        }

        private void OnEnable()
        {
            if (!All.Contains(this))
                All.Add(this);
            InteractionPromptBroker.Register(this);
        }

        private void OnDisable()
        {
            All.Remove(this);
            InteractionPromptBroker.Unregister(this);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag(playerTag) ||
                other.GetComponentInParent<Bayou.Player.BayouCharacterMotor>() != null)
            {
                _playerInRange = true;
                if (visualCue != null)
                    visualCue.SetActive(true);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag(playerTag) ||
                other.GetComponentInParent<Bayou.Player.BayouCharacterMotor>() != null)
            {
                _playerInRange = false;
                if (visualCue != null)
                    visualCue.SetActive(false);
            }
        }

        public bool TryGetInteractionPrompt(out InteractionPrompt prompt)
        {
            prompt = default;
            if (!_playerInRange || item == null) return false;

            var name = string.IsNullOrWhiteSpace(item.displayName) ? item.name : item.displayName;
            var action = string.IsNullOrWhiteSpace(pickupPrompt) ? $"Pick up {name}" : $"{pickupPrompt} {name}";
            var d = transform.position;
            var player = PlayerLocator.Transform;
            var dist = 0f;
            if (player != null)
            {
                var delta = d - player.position;
                delta.y = 0f;
                dist = delta.sqrMagnitude;
            }

            prompt = new InteractionPrompt("E", action.Trim(), 65, dist);
            return true;
        }

        private void Update()
        {
            if (!_playerInRange || item == null) return;

            var dialogue = DialogueManager.GetInstance();
            if (dialogue != null && dialogue.dialogueIsPlaying)
                return;

            var input = InputManager.GetInstance();
            if (input == null || !input.GetInteractPressed())
                return;

            var inv = InventoryController.Instance ?? FindFirstObjectByType<InventoryController>();
            if (item.IsUniqueEquipment && inv != null && inv.HasItemsById(item.Id, 1))
            {
                if (destroyOnPickup)
                    Destroy(gameObject);
                else
                    enabled = false;
                return;
            }

            if (addStraightToBag && item != null && !item.IsUniqueEquipment)
                GiveItemDirectly();
            else
                CaughtFishPresenter.Present(item);

            if (item.IsUniqueEquipment && (inv == null || !inv.HasItemsById(item.Id, 1)))
                return;

            if (endDemoOnPickup)
                DemoEndController.Show();

            if (destroyOnPickup)
                Destroy(gameObject);
            else
                enabled = false;
        }

        private void GiveItemDirectly()
        {
            var inv = InventoryController.Instance ?? FindFirstObjectByType<InventoryController>();
            if (inv == null || item == null) return;
            if (inv.HasItemsById(item.Id, 1)) return;
            if (!inv.TryAddItem(item) && !inv.TryHoldNewItem(item, out _))
                CaughtFishPresenter.Present(item);
        }
    }
}
