using Bayou.Player;
using Bayou.UI;
using UnityEngine;

namespace Bayou.Save
{
    [DisallowMultipleComponent]
    public sealed class BonfireInteractable : MonoBehaviour, IInteractionPromptSource
    {
        [SerializeField] private string bonfireId = "bonfire_01";
        [SerializeField] private string displayName = "Bayou Bonfire";
        [SerializeField] private BonfireUIController bonfireUi;
        [SerializeField] private GameObject visualCue;
        [SerializeField] private string restPrompt = "Rest / cook";

        private bool _playerInRange;

        private void Awake()
        {
            if (visualCue != null)
                visualCue.SetActive(false);

            if (bonfireUi == null)
                bonfireUi = FindFirstObjectByType<BonfireUIController>();
        }

        private void OnEnable() => InteractionPromptBroker.Register(this);
        private void OnDisable() => InteractionPromptBroker.Unregister(this);

        private void Update()
        {
            if (bonfireUi == null) return;

            var blocked = bonfireUi.IsOpen ||
                          (DialogueManager.GetInstance() != null && DialogueManager.GetInstance().dialogueIsPlaying);

            if (visualCue != null)
                visualCue.SetActive(_playerInRange && !blocked);

            if (!_playerInRange || blocked) return;

            var input = InputManager.GetInstance();
            if (input != null && input.GetInteractPressed())
                bonfireUi.Open(bonfireId, displayName);
        }

        public bool TryGetInteractionPrompt(out InteractionPrompt prompt)
        {
            prompt = default;
            if (!_playerInRange) return false;
            if (bonfireUi != null && bonfireUi.IsOpen) return false;

            var p = PlayerLocator.Transform;
            var dist = 0f;
            if (p != null)
            {
                var d = transform.position - p.position;
                d.y = 0f;
                dist = d.sqrMagnitude;
            }

            prompt = new InteractionPrompt("E", restPrompt, 55, dist);
            return true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.TryGetComponent<BayouCharacterMotor>(out _) ||
                other.GetComponentInParent<BayouCharacterMotor>() != null)
                _playerInRange = true;
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.TryGetComponent<BayouCharacterMotor>(out _) ||
                other.GetComponentInParent<BayouCharacterMotor>() != null)
                _playerInRange = false;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.45f, 0.1f, 0.85f);
            Gizmos.DrawWireSphere(transform.position, 0.75f);
        }
    }
}
