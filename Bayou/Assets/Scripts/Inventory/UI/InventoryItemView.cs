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
            PrepareGridRect(_rt);
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

            var shape = Item.definition.shape;
            if (_rt == null) _rt = GetComponent<RectTransform>();
            PrepareGridRect(_rt);
            if (_compartment != null)
            {
                _rt.sizeDelta = _compartment.GetItemSize(shape, Item.rotation);
                if (Item.IsPlaced)
                    _rt.anchoredPosition = _compartment.GridToAnchoredPosition(Item.gridX, Item.gridY, shape, Item.rotation);
            }

            if (iconImage != null)
            {
                FitIcon(iconImage, Item.definition.icon);
                iconImage.raycastTarget = true;
            }
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

        public static void PrepareGridRect(RectTransform rt)
        {
            if (rt == null) return;
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;
            rt.pivot = new Vector2(0f, 1f);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        }

        public static void FitIcon(Image icon, Sprite sprite)
        {
            if (icon == null) return;

            icon.type = Image.Type.Simple;
            icon.preserveAspect = true;
            icon.sprite = sprite;
            icon.enabled = sprite != null;
            icon.color = Color.white;

            var fitter = icon.GetComponent<AspectRatioFitter>();
            if (fitter != null)
                Object.Destroy(fitter);

            var rt = icon.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;

            if (sprite == null)
            {
                rt.sizeDelta = Vector2.zero;
                rt.anchoredPosition = Vector2.zero;
                return;
            }

            var parent = rt.parent as RectTransform;
            var plate = parent != null ? parent.rect.size : new Vector2(64f, 64f);
            if (plate.x < 4f || plate.y < 4f)
                plate = new Vector2(64f, 64f);
            var pad = 8f;
            var maxW = Mathf.Max(8f, plate.x - pad * 2f);
            var maxH = Mathf.Max(8f, plate.y - pad * 2f);
            var spriteSize = sprite.rect.size;
            if (spriteSize.x < 1f) spriteSize.x = 1f;
            if (spriteSize.y < 1f) spriteSize.y = 1f;
            var scale = Mathf.Min(maxW / spriteSize.x, maxH / spriteSize.y);
            rt.sizeDelta = spriteSize * scale;
            rt.anchoredPosition = Vector2.zero;
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
