using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Bayou.Inventory.UI
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class InventoryItemView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IInitializePotentialDragHandler
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

        /// <summary>Runtime shop templates: bind icon + background before <see cref="Init"/>.</summary>
        public void BindImages(Image icon, Image background)
        {
            iconImage = icon;
            backgroundImage = background;
        }

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

            if (backgroundImage == null)
                backgroundImage = GetComponent<Image>();
            if (iconImage == null)
            {
                var images = GetComponentsInChildren<Image>(true);
                foreach (var img in images)
                {
                    if (img != null && img != backgroundImage)
                    {
                        iconImage = img;
                        break;
                    }
                }
            }

            // No sprite => Unity generates no geometry => drag never starts.
            EnsureRaycastGraphic(backgroundImage);
            if (iconImage != null)
                iconImage.raycastTarget = true;

            SyncFromItem();
        }

        public void SetCompartment(InventoryCompartmentUI compartment) => _compartment = compartment;

        public void SyncFromItem()
        {
            if (Item?.definition == null) return;

            EnsureRaycastGraphic(backgroundImage);

            if (iconImage != null)
            {
                iconImage.sprite = Item.definition.icon;
                iconImage.enabled = true;
                iconImage.raycastTarget = true;
                if (Item.definition.icon == null)
                    iconImage.color = new Color(0.35f, 0.55f, 0.95f, 0.95f);
                else
                    iconImage.color = Color.white;
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

        public void OnInitializePotentialDrag(PointerEventData eventData)
        {
            eventData.useDragThreshold = false;
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

        private static void EnsureRaycastGraphic(Image image)
        {
            if (image == null) return;
            if (image.sprite == null)
                image.sprite = UiWhiteSprite.Get();
            image.raycastTarget = true;
            if (image.color.a < 0.01f)
            {
                var c = image.color;
                c.a = 0.85f;
                image.color = c;
            }
        }
    }
}
