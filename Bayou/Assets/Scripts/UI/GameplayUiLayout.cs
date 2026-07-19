using Bayou.Inventory.Shop;
using Bayou.Inventory.UI;
using UnityEngine;

namespace Bayou.UI
{
    /// <summary>
    /// Side-by-side layout: personal inventory (left) + shop merchant stock (right).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameplayUiLayout : MonoBehaviour
    {
        [Header("Split layout when shop is open")]
        [SerializeField] private float splitPanelWidth = 0.46f;
        [SerializeField] private float splitPanelHeight = 0.72f;
        [SerializeField] private float splitPanelMargin = 0.02f;

        private InventoryUIController _inventoryUi;
        private Vector2 _inventoryDefaultSize;
        private Vector2 _inventoryDefaultPosition;
        private Vector2 _inventoryDefaultAnchorMin;
        private Vector2 _inventoryDefaultAnchorMax;
        private Vector2 _inventoryDefaultPivot;
        private bool _hasInventoryDefaults;

        public static GameplayUiLayout Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
            _inventoryUi = FindFirstObjectByType<InventoryUIController>();
            CacheInventoryDefaults();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void ApplyShopSplitLayout(bool active)
        {
            if (_inventoryUi == null)
                _inventoryUi = FindFirstObjectByType<InventoryUIController>();

            if (_inventoryUi?.PanelRoot == null)
                return;

            CacheInventoryDefaults();
            var rt = _inventoryUi.PanelRoot;

            if (!active)
            {
                rt.anchorMin = _inventoryDefaultAnchorMin;
                rt.anchorMax = _inventoryDefaultAnchorMax;
                rt.pivot = _inventoryDefaultPivot;
                rt.sizeDelta = _inventoryDefaultSize;
                rt.anchoredPosition = _inventoryDefaultPosition;
                return;
            }

            var margin = splitPanelMargin;
            var w = splitPanelWidth;
            var h = splitPanelHeight;
            rt.anchorMin = new Vector2(margin, 0.5f - h * 0.5f);
            rt.anchorMax = new Vector2(margin + w, 0.5f + h * 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;

            if (!_inventoryUi.IsOpen)
                _inventoryUi.Open();
        }

        public void ApplyShopPanelAnchors(RectTransform merchantPanel, RectTransform shopPlayerPanel, bool hideShopPlayerPanel)
        {
            if (merchantPanel == null) return;

            var margin = splitPanelMargin;
            var w = splitPanelWidth;
            var h = splitPanelHeight;
            var leftEdge = 1f - margin - w;

            merchantPanel.anchorMin = new Vector2(leftEdge, 0.5f - h * 0.5f);
            merchantPanel.anchorMax = new Vector2(1f - margin, 0.5f + h * 0.5f);
            merchantPanel.offsetMin = Vector2.zero;
            merchantPanel.offsetMax = Vector2.zero;
            merchantPanel.anchoredPosition = Vector2.zero;

            if (shopPlayerPanel != null)
                shopPlayerPanel.gameObject.SetActive(!hideShopPlayerPanel);
        }

        private void CacheInventoryDefaults()
        {
            if (_hasInventoryDefaults || _inventoryUi?.PanelRoot == null) return;

            var rt = _inventoryUi.PanelRoot;
            _inventoryDefaultAnchorMin = rt.anchorMin;
            _inventoryDefaultAnchorMax = rt.anchorMax;
            _inventoryDefaultPivot = rt.pivot;
            _inventoryDefaultSize = rt.sizeDelta;
            _inventoryDefaultPosition = rt.anchoredPosition;
            _hasInventoryDefaults = true;
        }
    }
}
