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

        private Vector3 _homePosition;
        private Quaternion _homeRotation;
        private bool _playerInRange;

        public ItemDefinition Item => item;
        public static IReadOnlyList<QuestItemPickup> Living => All;

        private static readonly List<QuestItemPickup> All = new();
        private static readonly List<UniquePickupRecord> UniqueHomes = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            All.Clear();
            UniqueHomes.Clear();
        }

        private sealed class UniquePickupRecord
        {
            public ItemDefinition Item;
            public Vector3 Position;
            public Quaternion Rotation;
            public string Prompt;
            public QuestItemPickup Instance;
        }

        public void Bind(ItemDefinition definition, string prompt = "Pick up", bool addStraightToBag = false)
        {
            item = definition;
            this.addStraightToBag = addStraightToBag;
            if (!string.IsNullOrWhiteSpace(prompt))
                pickupPrompt = prompt;
            Bayou.Rendering.WorldItemVisual.EnsurePickupVisual(gameObject, item);
            CaptureHome();
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
            CaptureHome();
        }

        private void Start()
        {
            if (item != null && item.IsUniqueEquipment)
                addStraightToBag = false;
            Bayou.Rendering.WorldItemVisual.EnsurePickupVisual(gameObject, item);
            Bayou.Rendering.WorldItemVisual.SnapToGround(transform, 0.22f);
            CaptureHome();
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
                HideCollected();
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

            if (item.IsUniqueEquipment || ItemDefinition.IsLanternItem(item.Id))
                HideCollected();
            else if (destroyOnPickup)
                Destroy(gameObject);
            else
                enabled = false;
        }

        private void CaptureHome()
        {
            _homePosition = transform.position;
            _homeRotation = transform.rotation;
            RegisterUniqueHome();
        }

        private void RegisterUniqueHome()
        {
            if (item == null) return;
            if (!item.IsUniqueEquipment && !ItemDefinition.IsLanternItem(item.Id))
                return;

            for (var i = 0; i < UniqueHomes.Count; i++)
            {
                if (UniqueHomes[i].Instance != this) continue;
                UniqueHomes[i].Item = item;
                UniqueHomes[i].Position = _homePosition;
                UniqueHomes[i].Rotation = _homeRotation;
                UniqueHomes[i].Prompt = pickupPrompt;
                return;
            }

            UniqueHomes.Add(new UniquePickupRecord
            {
                Item = item,
                Position = _homePosition,
                Rotation = _homeRotation,
                Prompt = pickupPrompt,
                Instance = this
            });
        }

        private void HideCollected()
        {
            _playerInRange = false;
            gameObject.SetActive(false);
        }

        /// <summary>
        /// After death, put unique world pickups back if they are no longer in the bag.
        /// Stops the lantern (and similar quest gear) from vanishing forever.
        /// </summary>
        public static void RestoreMissingUniquePickups()
        {
            var inv = InventoryController.Instance ?? Object.FindFirstObjectByType<InventoryController>();
            for (var i = 0; i < UniqueHomes.Count; i++)
            {
                var home = UniqueHomes[i];
                if (home?.Item == null) continue;
                if (inv != null && inv.HasItemsById(home.Item.Id, 1))
                    continue;

                var pickup = home.Instance;
                if (pickup == null)
                {
                    pickup = SpawnUniquePickup(home);
                    home.Instance = pickup;
                    continue;
                }

                pickup._playerInRange = false;
                pickup.enabled = true;
                pickup.gameObject.SetActive(true);
                pickup.transform.SetPositionAndRotation(home.Position, home.Rotation);
                Bayou.Rendering.WorldItemVisual.EnsurePickupVisual(pickup.gameObject, home.Item);
            }
        }

        private static QuestItemPickup SpawnUniquePickup(UniquePickupRecord home)
        {
            var go = new GameObject(home.Item.displayName + "Pickup");
            go.transform.SetPositionAndRotation(home.Position, home.Rotation);
            var col = go.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 1.7f;
            var pickup = go.AddComponent<QuestItemPickup>();
            pickup.Bind(home.Item, string.IsNullOrWhiteSpace(home.Prompt) ? "Pick up" : home.Prompt, addStraightToBag: false);
            pickup.destroyOnPickup = true;
            return pickup;
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
