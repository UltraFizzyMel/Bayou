using Bayou.Inventory;
using Bayou.Inventory.UI;
using UnityEngine;

namespace Bayou.UI
{
    /// <summary>
    /// Side-by-side layout: personal inventory (left) + shop merchant stock (right).
    /// Supports both procedural <see cref="InventoryUIController"/> and handmade <see cref="InventoryDisplayUI"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameplayUiLayout : MonoBehaviour
    {
        [Header("Split layout when shop is open")]
        [SerializeField] private float splitPanelWidth = 0.46f;
        [SerializeField] private float splitPanelHeight = 0.72f;
        [SerializeField] private float splitPanelMargin = 0.02f;

        private InventoryUIController _inventoryUi;
        private InventoryDisplayUI _handmadeUi;
        private RectTransformDefaults _proceduralDefaults;
        private RectTransformDefaults _handmadeDefaults;
        private bool _hasProceduralDefaults;
        private bool _hasHandmadeDefaults;
        private Canvas _handmadeCanvas;
        private int _handmadeCanvasDefaultSort;
        private bool _hasHandmadeCanvasSort;

        private const int ShopSessionInventorySortOrder = 25;

        public static GameplayUiLayout Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
            _inventoryUi = FindFirstObjectByType<InventoryUIController>();
            _handmadeUi = InventoryDisplayUI.Active ?? FindFirstObjectByType<InventoryDisplayUI>();
            CacheDefaults();
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
            if (_handmadeUi == null)
                _handmadeUi = InventoryDisplayUI.Active ?? FindFirstObjectByType<InventoryDisplayUI>();

            CacheDefaults();

            if (_handmadeUi?.PanelRoot != null)
            {
                ApplyDock(_handmadeUi.PanelRoot, ref _handmadeDefaults, ref _hasHandmadeDefaults, active, leftSide: true);
                SetHandmadeCanvasAboveShop(active);
                if (active && !_handmadeUi.IsOpen)
                    _handmadeUi.Open();
                return;
            }

            if (_inventoryUi?.PanelRoot == null)
                return;

            ApplyDock(_inventoryUi.PanelRoot, ref _proceduralDefaults, ref _hasProceduralDefaults, active, leftSide: true);
            if (active && !_inventoryUi.IsOpen)
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
            merchantPanel.pivot = new Vector2(0.5f, 0.5f);
            merchantPanel.offsetMin = Vector2.zero;
            merchantPanel.offsetMax = Vector2.zero;
            merchantPanel.anchoredPosition = Vector2.zero;

            if (shopPlayerPanel != null)
                shopPlayerPanel.gameObject.SetActive(!hideShopPlayerPanel);
        }

        private void ApplyDock(
            RectTransform rt,
            ref RectTransformDefaults defaults,
            ref bool hasDefaults,
            bool active,
            bool leftSide)
        {
            if (!hasDefaults)
            {
                defaults = RectTransformDefaults.Capture(rt);
                hasDefaults = true;
            }

            if (!active)
            {
                defaults.Restore(rt);
                return;
            }

            var margin = splitPanelMargin;
            var w = splitPanelWidth;
            var h = splitPanelHeight;
            if (leftSide)
            {
                rt.anchorMin = new Vector2(margin, 0.5f - h * 0.5f);
                rt.anchorMax = new Vector2(margin + w, 0.5f + h * 0.5f);
            }
            else
            {
                var leftEdge = 1f - margin - w;
                rt.anchorMin = new Vector2(leftEdge, 0.5f - h * 0.5f);
                rt.anchorMax = new Vector2(1f - margin, 0.5f + h * 0.5f);
            }

            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
            rt.localScale = Vector3.one;
        }

        private void CacheDefaults()
        {
            if (!_hasProceduralDefaults && _inventoryUi?.PanelRoot != null)
            {
                _proceduralDefaults = RectTransformDefaults.Capture(_inventoryUi.PanelRoot);
                _hasProceduralDefaults = true;
            }

            if (!_hasHandmadeDefaults && _handmadeUi?.PanelRoot != null)
            {
                _handmadeDefaults = RectTransformDefaults.Capture(_handmadeUi.PanelRoot);
                _hasHandmadeDefaults = true;
            }
        }

        /// <summary>
        /// Keep the player bag above the shop dim so it stays visible and receives drag input for selling.
        /// </summary>
        private void SetHandmadeCanvasAboveShop(bool active)
        {
            if (_handmadeUi == null) return;

            if (_handmadeCanvas == null)
                _handmadeCanvas = _handmadeUi.GetComponentInParent<Canvas>();
            if (_handmadeCanvas == null) return;

            if (!_hasHandmadeCanvasSort)
            {
                _handmadeCanvasDefaultSort = _handmadeCanvas.sortingOrder;
                _hasHandmadeCanvasSort = true;
            }

            _handmadeCanvas.sortingOrder = active
                ? ShopSessionInventorySortOrder
                : _handmadeCanvasDefaultSort;
        }

        private struct RectTransformDefaults
        {
            public Vector2 anchorMin;
            public Vector2 anchorMax;
            public Vector2 pivot;
            public Vector2 sizeDelta;
            public Vector2 anchoredPosition;
            public Vector2 offsetMin;
            public Vector2 offsetMax;

            public static RectTransformDefaults Capture(RectTransform rt) => new()
            {
                anchorMin = rt.anchorMin,
                anchorMax = rt.anchorMax,
                pivot = rt.pivot,
                sizeDelta = rt.sizeDelta,
                anchoredPosition = rt.anchoredPosition,
                offsetMin = rt.offsetMin,
                offsetMax = rt.offsetMax
            };

            public void Restore(RectTransform rt)
            {
                rt.anchorMin = anchorMin;
                rt.anchorMax = anchorMax;
                rt.pivot = pivot;
                // Centered bag used sizeDelta; stretch dock used offsets — restore both.
                rt.offsetMin = offsetMin;
                rt.offsetMax = offsetMax;
                rt.sizeDelta = sizeDelta;
                rt.anchoredPosition = anchoredPosition;
            }
        }
    }
}
