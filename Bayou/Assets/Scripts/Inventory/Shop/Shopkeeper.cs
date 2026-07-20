using UnityEngine;

namespace Bayou.Inventory.Shop
{
    /// <summary>
    /// Holds Caliste's <see cref="ShopDefinition"/>. Prefer opening via dialogue (<c>OpenShop()</c> in Ink).
    /// Optional proximity interact is off by default so talking to Caliste is the trigger.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Shopkeeper : MonoBehaviour
    {
        [SerializeField] private ShopDefinition shop;
        [SerializeField] private ShopUIController shopUi;
        [SerializeField] private GameObject visualCue;
        [SerializeField] private float interactRadius = 3f;
        [SerializeField] private string playerTag = "Player";
        [Tooltip("If on, pressing Interact near this object opens the shop (debug). Prefer Caliste dialogue.")]
        [SerializeField] private bool openOnInteract;

        public ShopDefinition ShopDefinition => shop;

        private Transform _player;
        private bool _playerInRange;

        private void Start()
        {
            if (shopUi == null)
                shopUi = FindFirstObjectByType<ShopUIController>();

            if (visualCue != null)
                visualCue.SetActive(false);

            var playerGo = GameObject.FindGameObjectWithTag(playerTag);
            if (playerGo != null)
                _player = playerGo.transform;
        }

        private void Update()
        {
            if (!openOnInteract || shopUi == null || shop == null) return;

            if (_player != null)
            {
                var dist = Vector3.Distance(transform.position, _player.position);
                _playerInRange = dist <= interactRadius;
            }

            var blocked = shopUi.IsOpen ||
                          (DialogueManager.GetInstance() != null && DialogueManager.GetInstance().dialogueIsPlaying);

            if (visualCue != null)
                visualCue.SetActive(_playerInRange && !blocked);

            if (!_playerInRange || blocked) return;

            var input = InputManager.GetInstance();
            if (input != null && input.GetInteractPressed())
                Open();
        }

        public void Open()
        {
            if (shopUi == null)
                shopUi = ShopUiBuilder.EnsureInScene(shop) ?? FindFirstObjectByType<ShopUIController>();
            if (shopUi == null || shop == null)
            {
                Debug.LogWarning("[Shopkeeper] Missing shop UI or ShopDefinition.");
                return;
            }

            var handmade = InventoryDisplayUI.Active ?? FindFirstObjectByType<InventoryDisplayUI>();
            if (handmade != null)
                shopUi.AssignHandmadeInventory(handmade);

            shopUi.OpenShop(shop);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.9f, 0.75f, 0.2f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, interactRadius);
        }
    }
}
