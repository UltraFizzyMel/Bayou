using System.Collections.Generic;
using Bayou.Inventory;
using Bayou.Save;
using Bayou;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Bayou.Save
{
    [DisallowMultipleComponent]
    public sealed class BonfireUIController : MonoBehaviour
    {
        [SerializeField] private RectTransform overlayRoot;
        [SerializeField] private TextMeshProUGUI titleLabel;
        [SerializeField] private TextMeshProUGUI hintLabel;
        [SerializeField] private TextMeshProUGUI statusLabel;
        [SerializeField] private RectTransform fishListRoot;
        [SerializeField] private Button fishEntryTemplate;
        [SerializeField] private Button cookAndRestButton;
        [SerializeField] private Button cancelButton;

        private readonly List<Button> _fishButtons = new();
        private InventoryController _inventory;
        private GameSaveSystem _saveSystem;
        private string _bonfireId;
        private InventoryItemInstance _selectedFish;
        private bool _isOpen;

        public static BonfireUIController Active { get; private set; }
        public bool IsOpen => _isOpen;

        private void Awake()
        {
            if (overlayRoot != null)
                overlayRoot.gameObject.SetActive(false);

            if (fishEntryTemplate != null)
                fishEntryTemplate.gameObject.SetActive(false);

            cookAndRestButton?.onClick.AddListener(OnCookAndRest);
            cancelButton?.onClick.AddListener(Close);
        }

        private void OnDestroy()
        {
            cookAndRestButton?.onClick.RemoveListener(OnCookAndRest);
            cancelButton?.onClick.RemoveListener(Close);
            if (Active == this)
                Active = null;
        }

        public void Open(string bonfireId, string bonfireDisplayName = "Bonfire")
        {
            _inventory = InventoryController.Instance;
            _saveSystem = GameSaveSystem.Instance;
            _bonfireId = bonfireId;
            _selectedFish = null;
            _isOpen = true;
            Active = this;

            if (titleLabel != null)
                titleLabel.text = bonfireDisplayName;
            if (hintLabel != null)
                hintLabel.text = "Select a fish from your pack to cook. Resting at the fire saves your journey.";
            if (statusLabel != null)
                statusLabel.text = string.Empty;

            if (overlayRoot != null)
                overlayRoot.gameObject.SetActive(true);

            GameplayPause.SyncFromUiState();
            RefreshFishList();
            UpdateCookButton();
        }

        public void Close()
        {
            _isOpen = false;
            _selectedFish = null;
            if (Active == this)
                Active = null;

            if (overlayRoot != null)
                overlayRoot.gameObject.SetActive(false);

            GameplayPause.SyncFromUiState();

            ClearFishButtons();
        }

        private void RefreshFishList()
        {
            ClearFishButtons();
            if (_inventory == null || fishListRoot == null || fishEntryTemplate == null)
                return;

            var fish = _inventory.GetFishItems();
            if (fish.Count == 0)
            {
                if (statusLabel != null)
                    statusLabel.text = "You have no fish to cook. Catch one in the bayou first.";
                return;
            }

            if (statusLabel != null)
                statusLabel.text = "Choose a fish to sacrifice to the fire.";

            foreach (var item in fish)
            {
                var btn = Instantiate(fishEntryTemplate, fishListRoot);
                btn.gameObject.SetActive(true);
                var label = btn.GetComponentInChildren<TextMeshProUGUI>();
                if (label != null)
                    label.text = item.definition.displayName;

                var captured = item;
                btn.onClick.AddListener(() => SelectFish(captured, btn));
                _fishButtons.Add(btn);
            }
        }

        private void SelectFish(InventoryItemInstance fish, Button button)
        {
            _selectedFish = fish;
            foreach (var btn in _fishButtons)
            {
                if (btn == null) continue;
                var img = btn.targetGraphic as Image;
                if (img != null)
                    img.color = btn == button
                        ? new Color(0.35f, 0.55f, 0.38f, 1f)
                        : new Color(0.22f, 0.24f, 0.28f, 1f);
            }

            UpdateCookButton();
        }

        private void UpdateCookButton()
        {
            if (cookAndRestButton == null) return;
            cookAndRestButton.interactable = _selectedFish != null && _saveSystem != null;
        }

        private void OnCookAndRest()
        {
            if (_selectedFish == null || _inventory == null || _saveSystem == null)
                return;

            if (!_saveSystem.Save(_bonfireId))
            {
                if (statusLabel != null)
                    statusLabel.text = "The fire sputtered out. Save failed.";
                return;
            }

            var cooked = _selectedFish;
            _selectedFish = null;
            _inventory.RemoveItem(cooked);

            if (statusLabel != null)
                statusLabel.text = "Fish cooked. Your progress is saved.";

            Invoke(nameof(Close), 1.2f);
        }

        private void ClearFishButtons()
        {
            foreach (var btn in _fishButtons)
            {
                if (btn != null)
                    Destroy(btn.gameObject);
            }
            _fishButtons.Clear();
        }
    }
}
