using Bayou.Demo;
using Bayou.Inventory;
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

        private bool _playerInRange;

        private void Reset()
        {
            var col = GetComponent<Collider>();
            if (col != null)
                col.isTrigger = true;
        }

        private void OnEnable() => InteractionPromptBroker.Register(this);
        private void OnDisable() => InteractionPromptBroker.Unregister(this);

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
            var player = GameObject.FindGameObjectWithTag("Player");
            var dist = 0f;
            if (player != null)
            {
                var delta = d - player.transform.position;
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

            CaughtFishPresenter.Present(item);

            if (endDemoOnPickup)
                DemoEndController.Show();

            if (destroyOnPickup)
                Destroy(gameObject);
            else
                enabled = false;
        }
    }
}
