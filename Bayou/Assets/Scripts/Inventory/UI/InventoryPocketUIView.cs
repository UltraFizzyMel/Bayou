using Bayou.Inventory;
using UnityEngine;
using UnityEngine.UI;

namespace Bayou.Inventory.UI
{
    /// <summary>
    /// One compartment on the panel — lock overlay only when compartment upgrades are enabled.
    /// </summary>
    public sealed class InventoryPocketUIView : MonoBehaviour
    {
        [SerializeField] private string compartmentId;
        [SerializeField] private RectTransform gridRoot;
        [SerializeField] private RectTransform itemsRoot;
        [SerializeField] private GameObject lockOverlay;
        [SerializeField] private Text lockLabel;

        private InventoryCompartmentConfig _config;
        private InventoryUIController _ui;
        private bool _cellsBuilt;
        private bool _fillPanel;

        public string CompartmentId => compartmentId;
        public RectTransform GridRoot => gridRoot;
        public RectTransform ItemsRoot => itemsRoot;
        public InventoryCompartmentUI CompartmentUI { get; private set; }

        public void Setup(
            InventoryUIController ui,
            InventoryCompartmentConfig config,
            RectTransform grid,
            RectTransform items,
            bool fillPanel)
        {
            _ui = ui;
            _config = config;
            _fillPanel = fillPanel;
            compartmentId = config.id;
            gridRoot = grid;
            itemsRoot = items;
            EnsureLockOverlay();
            RefreshUnlockState();
        }

        public void SetCompartmentUI(InventoryCompartmentUI compartmentUi) => CompartmentUI = compartmentUi;

        public void RefreshUnlockState()
        {
            if (_ui == null || _config == null) return;

            var inventory = _ui.Inventory;
            var upgradesOn = inventory != null && inventory.CompartmentUpgradesEnabled;
            var unlocked = inventory == null || inventory.IsCompartmentUnlocked(compartmentId);

            if (lockOverlay != null)
                lockOverlay.SetActive(upgradesOn && !unlocked);

            if (gridRoot != null)
                gridRoot.gameObject.SetActive(unlocked);

            if (itemsRoot != null)
                itemsRoot.gameObject.SetActive(unlocked);

            if (unlocked && !_cellsBuilt && CompartmentUI != null)
            {
                _ui.BuildCellsForPocket(this, _config, CompartmentUI, _fillPanel);
                _cellsBuilt = true;
            }
        }

        private void EnsureLockOverlay()
        {
            if (lockOverlay != null) return;

            lockOverlay = new GameObject("LockOverlay", typeof(RectTransform));
            var rt = lockOverlay.GetComponent<RectTransform>();
            rt.SetParent(transform, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var img = lockOverlay.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0.55f);
            img.raycastTarget = true;

            var labelGo = new GameObject("Label", typeof(RectTransform));
            var labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.SetParent(rt, false);
            labelRt.anchorMin = labelRt.anchorMax = new Vector2(0.5f, 0.5f);
            labelRt.sizeDelta = new Vector2(120, 40);

            lockLabel = labelGo.AddComponent<Text>();
            lockLabel.alignment = TextAnchor.MiddleCenter;
            lockLabel.fontSize = 14;
            lockLabel.text = "LOCKED";
            lockLabel.color = new Color(1f, 0.9f, 0.7f, 0.9f);
            lockLabel.raycastTarget = false;
        }
    }
}
