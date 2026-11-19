using System.Collections.Generic;
using Bayou.Demo;
using Bayou.Environment;
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
        // Placed cargo boxes already have their own meshes and scene positions.
        [SerializeField] private bool preservePlacedCargoVisual;
        private const float ProximityPickupRange = 2.35f;
        private const float ProximityMaxHeight = 3f;

        private Vector3 _homePosition;
        private Quaternion _homeRotation;
        private bool _playerInRange;

        public ItemDefinition Item => item;
        public bool PreservePlacedCargoVisual => preservePlacedCargoVisual;
        public static IReadOnlyList<QuestItemPickup> Living => All;

        /// <summary>A quest pickup the player is already standing on, at least as close as <paramref name="distSq"/>.</summary>
        public static bool HasPickupAtMost(float distSq)
        {
            var player = PlayerLocator.Transform;
            for (var i = 0; i < All.Count; i++)
            {
                var pickup = All[i];
                if (pickup == null || !pickup.isActiveAndEnabled || !pickup.PlayerInPickupRange() || pickup.item == null)
                    continue;
                if (player == null) return true;
                var delta = pickup.transform.position - player.position;
                delta.y = 0f;
                if (delta.sqrMagnitude <= distSq)
                    return true;
            }

            return false;
        }

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

        /// <summary>
        /// Editor-only scene setup entry point for Caliste's three cargo crates.
        /// Keep the artist's placed mesh/materials and do not snap its transform to terrain.
        /// </summary>
        public void ConfigureAsCargoBox(ItemDefinition definition)
        {
            item = definition;
            pickupPrompt = "Pick up";
            addStraightToBag = true;
            destroyOnPickup = true;
            endDemoOnPickup = false;
            preservePlacedCargoVisual = true;
            CaptureHome();
        }

        private bool PlayerInPickupRange()
        {
            if (_playerInRange && !preservePlacedCargoVisual) return true;
            // Physics trigger callbacks can be missed for objects placed in the editor,
            // especially when the player starts inside the trigger or the collider has
            // no matching Rigidbody. A short proximity check makes E reliable.
            var player = PlayerLocator.Transform;
            if (player == null) return false;
            var d = transform.position - player.position;
            if (Mathf.Abs(d.y) > ProximityMaxHeight) return false;
            d.y = 0f;
            return d.sqrMagnitude <= ProximityPickupRange * ProximityPickupRange;
        }

        private void Reset()
        {
            // Keep artist-placed solid colliders solid so players cannot walk
            // through crates. The setup menu provides a separate trigger sphere.
            var colliders = GetComponents<Collider>();
            foreach (var collider in colliders)
                if (collider != null && collider.isTrigger) return;
            var trigger = gameObject.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 1.7f;
        }

        private void Awake()
        {
            if (!preservePlacedCargoVisual)
                Bayou.Rendering.WorldItemVisual.EnsurePickupVisual(gameObject, item);
            CaptureHome();
        }

        private void Start()
        {
            if (item != null && item.IsUniqueEquipment)
                addStraightToBag = false;
            if (!preservePlacedCargoVisual)
            {
                Bayou.Rendering.WorldItemVisual.EnsurePickupVisual(gameObject, item);
                Bayou.Rendering.WorldItemVisual.SnapToGround(transform, 0.22f);
            }
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

        private void OnTriggerStay(Collider other)
        {
            if (other.CompareTag(playerTag) ||
                other.GetComponentInParent<Bayou.Player.BayouCharacterMotor>() != null)
                _playerInRange = true;
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
            if (item == null || WasCargoReturned() || !PlayerInPickupRange()) return false;

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
            if (item == null) return;
            if (WasCargoReturned())
            {
                HideCollected();
                return;
            }
            if (!PlayerInPickupRange()) return;
            if (Bayou.GameplayPause.BlocksWorldInteract || SignReadUi.IsOpen) return;

            var dialogue = DialogueManager.GetInstance();
            if (dialogue != null && dialogue.dialogueIsPlaying)
                return;

            var player = PlayerLocator.Transform;
            if (player != null)
            {
                var delta = transform.position - player.position;
                delta.y = 0f;
                if (DialogueTrigger.HasTalkAtMost(delta.sqrMagnitude))
                    return;
            }

            var input = InputManager.GetInstance();
            if (input == null || !input.GetInteractPressed())
                return;

            var inv = InventoryController.Instance ?? FindFirstObjectByType<InventoryController>();
            if (item.IsUniqueEquipment && inv != null && inv.HasItemsById(item.Id, 1))
            {
                HideCollected();
                return;
            }

            if ((addStraightToBag || IsCargoBox()) && !item.IsUniqueEquipment)
            {
                // Do not remove a world pickup unless the inventory actually accepted it.
                if (!GiveItemDirectly()) return;
            }
            else
            {
                CaughtFishPresenter.Present(item);
                if (item.IsUniqueEquipment && (inv == null || !inv.HasItemsById(item.Id, 1)))
                    return;
            }

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

        private bool WasCargoReturned()
        {
            if (!IsCargoBox()) return false;
            if (item.MatchesId("Item_CalisteCargoGraveyard")) return WorldFlags.Has(WorldFlags.FoundBoxGraveyard);
            if (item.MatchesId("Item_CalisteCargoFoggy")) return WorldFlags.Has(WorldFlags.FoundBoxFoggyMarsh);
            return item.MatchesId("Item_CalisteCargoBrackish") && WorldFlags.Has(WorldFlags.FoundBoxBrackishShore);
        }

        private bool IsCargoBox() => preservePlacedCargoVisual ||
            item.Id.StartsWith("Item_CalisteCargo", System.StringComparison.OrdinalIgnoreCase);

        private bool GiveItemDirectly()
        {
            var inv = InventoryController.Instance ?? FindFirstObjectByType<InventoryController>();
            if (inv == null || item == null)
            {
                Debug.LogWarning($"[QuestPickup] Can't collect '{name}': inventory or item definition missing.", this);
                return false;
            }
            // A collected unique cargo ID must never create a second copy.
            if (inv.HasItemsById(item.Id, 1)) return true;
            if (inv.TryAddItem(item)) return true;
            if (!IsCargoBox() && inv.TryHoldNewItem(item, out _)) return true;
            Bayou.UI.GameplayHud.Instance?.ShowNotice("Your inventory is full.");
            Debug.LogWarning($"[QuestPickup] Couldn't put '{item.Id}' in the bag. Pickup remains in the world.", this);
            return false;
        }
    }
}
