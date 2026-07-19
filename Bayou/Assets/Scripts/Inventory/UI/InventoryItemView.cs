using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Bayou.Inventory.UI
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class InventoryItemView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private Image backgroundImage;

        private IInventoryDragHost _ui;
        private InventoryCompartmentUI _compartment;
        private RectTransform _rt;
        private CanvasGroup _canvasGroup;

        public InventoryItemInstance Item { get; private set; }
        public InventoryCompartmentUI Compartment => _compartment;
        public RectTransform RectTransform => _rt;

        public void Init(IInventoryDragHost ui, InventoryItemInstance item, InventoryCompartmentUI compartment)
        {
            _ui = ui;
            Item = item;
            _compartment = compartment;
            _rt = GetComponent<RectTransform>();
            _rt.pivot = new Vector2(0, 1);
            _rt.anchorMin = _rt.anchorMax = new Vector2(0, 1);
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();

            if (iconImage == null)
                iconImage = GetComponentInChildren<Image>();

            if (backgroundImage != null)
                backgroundImage.raycastTarget = true;
            if (iconImage != null)
                iconImage.raycastTarget = true;

            SyncFromItem();
        }

        public void SetCompartment(InventoryCompartmentUI compartment) => _compartment = compartment;

        public void SyncFromItem()
        {
            if (Item?.definition == null) return;

            if (iconImage != null)
            {
                iconImage.sprite = Item.definition.icon;
                iconImage.enabled = Item.definition.icon != null;
            }

            var shape = Item.definition.shape;
            if (_compartment == null) return;

            _rt.sizeDelta = _compartment.GetItemSize(shape, Item.rotation);
            if (Item.IsPlaced)
                _rt.anchoredPosition = _compartment.GridToAnchoredPosition(Item.gridX, Item.gridY, shape, Item.rotation);
        }

        public void RotateClockwise()
        {
            if (Item == null) return;
            Item.rotation = (Item.rotation + 1) % 4;
            SyncFromItem();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_canvasGroup != null)
                _canvasGroup.blocksRaycasts = false;
            _ui?.BeginDrag(this);
        }

        public void OnDrag(PointerEventData eventData) => _ui?.Dragging(this, eventData);

        public void OnEndDrag(PointerEventData eventData)
        {
            _ui?.EndDrag(this, eventData);
            if (_canvasGroup != null)
                _canvasGroup.blocksRaycasts = true;
        }
    }
}
