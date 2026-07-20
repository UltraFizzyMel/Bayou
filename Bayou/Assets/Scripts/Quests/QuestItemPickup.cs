using Bayou.Demo;
using Bayou.Inventory;
using UnityEngine;

namespace Bayou.Quests
{
    /// <summary>
    /// World pickup: stand in trigger and press Interact (E) to add an item to the bag.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class QuestItemPickup : MonoBehaviour
    {
        [SerializeField] private ItemDefinition item;
        [SerializeField] private string playerTag = "Player";
        [SerializeField] private bool destroyOnPickup = true;
        [SerializeField] private GameObject visualCue;
        [SerializeField] private string pickupPrompt = "Pick up";
        [Tooltip("If true, picking this up finishes the demo (lantern).")]
        [SerializeField] private bool endDemoOnPickup;

        private bool _playerInRange;

        private void Reset()
        {
            var col = GetComponent<Collider>();
            if (col != null)
                col.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag(playerTag))
            {
                _playerInRange = true;
                if (visualCue != null)
                    visualCue.SetActive(true);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag(playerTag))
            {
                _playerInRange = false;
                if (visualCue != null)
                    visualCue.SetActive(false);
            }
        }

        private void Update()
        {
            if (!_playerInRange || item == null) return;

            var input = InputManager.GetInstance();
            if (input == null || !input.GetInteractPressed())
                return;

            var inv = InventoryController.Instance;
            if (inv == null)
            {
                Debug.LogWarning("[QuestItemPickup] No InventoryController.");
                return;
            }

            if (!inv.TryAddItem(item) && !inv.TryHoldNewItem(item, out _))
            {
                Debug.LogWarning($"[QuestItemPickup] Could not add {item.displayName} — bag full?");
                return;
            }

            Debug.Log($"[QuestItemPickup] Picked up {item.displayName} ({pickupPrompt}).");

            if (endDemoOnPickup)
                DemoEndController.Show();

            if (destroyOnPickup)
                Destroy(gameObject);
            else
                enabled = false;
        }
    }
}
