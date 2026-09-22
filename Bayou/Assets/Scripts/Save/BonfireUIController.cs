using System.Collections.Generic;
using Bayou.Inventory;
using Bayou.Player;
using Bayou.Save;
using Bayou;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

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
        public bool IsOpen =>
            _isOpen && (overlayRoot == null || overlayRoot.gameObject.activeInHierarchy);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Active = null;

        private void Awake()
        {
            _isOpen = false;
            if (Active == this)
                Active = null;

            if (overlayRoot != null)
                overlayRoot.gameObject.SetActive(false);

            if (fishEntryTemplate != null)
                fishEntryTemplate.gameObject.SetActive(false);

            cookAndRestButton?.onClick.AddListener(OnCookAndRest);
            cancelButton?.onClick.AddListener(Close);
            SetRestButtonLabel("Rest");
        }

        private void OnDestroy()
        {
            cookAndRestButton?.onClick.RemoveListener(OnCookAndRest);
            cancelButton?.onClick.RemoveListener(Close);
            if (Active == this)
                Active = null;
        }

        public void Open(string bonfireId, string bonfireDisplayName = "Campfire")
        {
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);
            if (transform.localScale.sqrMagnitude < 0.01f)
                transform.localScale = Vector3.one;

            _inventory = InventoryController.Instance;
            _saveSystem = GameSaveSystem.Instance;
            _bonfireId = bonfireId;
            _selectedFish = null;
            _isOpen = true;
            Active = this;

            if (titleLabel != null)
                titleLabel.text = bonfireDisplayName;
            if (hintLabel != null)
                hintLabel.text = "Cooking a fish is the only way to restore health. Resting without a fish still saves.";
            if (statusLabel != null)
                statusLabel.text = string.Empty;

            if (overlayRoot != null)
            {
                overlayRoot.gameObject.SetActive(true);
                var dim = overlayRoot.GetComponent<Image>();
                if (dim != null)
                    dim.raycastTarget = true;
            }

            var canvas = GetComponent<Canvas>() ?? overlayRoot?.GetComponentInParent<Canvas>();
            if (canvas != null)
                canvas.sortingOrder = Mathf.Max(canvas.sortingOrder, 55);

            ClosePlayerInventory();

            var fireAudio = Bayou.Audio.BonfireAudio.Resolve();
            fireAudio?.PlayStrikeMatch();
            fireAudio?.StartBurningLoop();

            GameplayPause.SyncFromUiState();
            RefreshFishList();
            UpdateCookButton();
        }

        private void Update()
        {
            if (!_isOpen) return;
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
                Close();
#endif
        }

        private static void ClosePlayerInventory()
        {
            var handmade = InventoryDisplayUI.Active ?? Object.FindFirstObjectByType<InventoryDisplayUI>();
            if (handmade != null && handmade.IsOpen)
                handmade.Close();

            var procedural = Object.FindFirstObjectByType<Bayou.Inventory.UI.InventoryUIController>();
            if (procedural != null && procedural.IsOpen)
                procedural.Close();
        }

        public void Close()
        {
            Bayou.Audio.BonfireAudio.Resolve()?.StopBurningLoop();

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

            EnsureFishListLayout();

            var fish = _inventory.GetFishItems();
            if (fish.Count == 0)
            {
                if (statusLabel != null)
                    statusLabel.text = "No fish to cook. Rest to save — you will not heal.";
                UpdateCookButton();
                return;
            }

            if (statusLabel != null)
                statusLabel.text = "Choose a fish to sacrifice to the fire.";

            foreach (var item in fish)
            {
                var btn = Instantiate(fishEntryTemplate, fishListRoot);
                StyleFishButton(btn, item);
                var captured = item;
                btn.onClick.AddListener(() => SelectFish(captured, btn));
                _fishButtons.Add(btn);
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(fishListRoot);
        }

        private void EnsureFishListLayout()
        {
            var layout = fishListRoot.GetComponent<VerticalLayoutGroup>();
            if (layout == null)
                layout = fishListRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.padding = new RectOffset(0, 0, 4, 4);
        }

        private static void StyleFishButton(Button btn, InventoryItemInstance item)
        {
            btn.gameObject.SetActive(true);
            var rt = btn.transform as RectTransform;
            if (rt != null)
            {
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.sizeDelta = new Vector2(0f, 40f);
                rt.localScale = Vector3.one;
            }

            var layout = btn.GetComponent<LayoutElement>() ?? btn.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = 40f;
            layout.preferredHeight = 40f;
            layout.flexibleHeight = 0f;
            layout.minWidth = 80f;

            var label = btn.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null)
            {
                label.text = item?.definition != null ? item.definition.displayName : "Fish";
                label.raycastTarget = false;
                var labelRt = label.rectTransform;
                labelRt.anchorMin = Vector2.zero;
                labelRt.anchorMax = Vector2.one;
                labelRt.offsetMin = new Vector2(10f, 4f);
                labelRt.offsetMax = new Vector2(-10f, -4f);
            }

            var img = btn.targetGraphic as Image;
            if (img != null)
                img.color = new Color(0.22f, 0.24f, 0.28f, 1f);
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
            cookAndRestButton.interactable = _saveSystem != null;
            SetRestButtonLabel(_selectedFish != null ? "Cook & Rest" : "Rest");
        }

        private void SetRestButtonLabel(string label)
        {
            if (cookAndRestButton == null) return;
            var tmp = cookAndRestButton.GetComponentInChildren<TextMeshProUGUI>(true);
            if (tmp != null)
                tmp.text = label;
        }

        private void OnCookAndRest()
        {
            if (_inventory == null || _saveSystem == null)
                return;

            var cooked = _selectedFish != null;
            if (cooked)
            {
                var meal = _selectedFish;
                _selectedFish = null;
                _inventory.RemoveItem(meal);
                PlayerHealth.Resolve()?.HealToFull();
            }

            if (!_saveSystem.Save(_bonfireId))
            {
                if (statusLabel != null)
                    statusLabel.text = "The fire sputtered out. Save failed.";
                return;
            }

            if (statusLabel != null)
            {
                var health = PlayerHealth.Resolve();
                var wounded = health != null && health.Current < health.Max;
                statusLabel.text = cooked
                    ? "The meal restores you. Your progress is saved."
                    : wounded
                        ? "You rest. Your progress is saved, but you are still wounded."
                        : "You rest. Your progress is saved.";
            }

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
