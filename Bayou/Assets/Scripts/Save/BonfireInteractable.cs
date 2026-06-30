using Bayou.Player;
using UnityEngine;

namespace Bayou.Save
{
    [DisallowMultipleComponent]
    public sealed class BonfireInteractable : MonoBehaviour
    {
        [SerializeField] private string bonfireId = "bonfire_01";
        [SerializeField] private string displayName = "Bayou Bonfire";
        [SerializeField] private BonfireUIController bonfireUi;
        [SerializeField] private GameObject visualCue;

        private bool _playerInRange;

        private void Awake()
        {
            if (visualCue != null)
                visualCue.SetActive(false);

            if (bonfireUi == null)
                bonfireUi = FindFirstObjectByType<BonfireUIController>();
        }

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

        private void OnTriggerEnter(Collider other)
        {
            if (other.TryGetComponent<BayouCharacterMotor>(out _))
                _playerInRange = true;
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.TryGetComponent<BayouCharacterMotor>(out _))
                _playerInRange = false;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.45f, 0.1f, 0.85f);
            Gizmos.DrawWireSphere(transform.position, 0.75f);
        }
    }
}
