using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Bayou.Inventory
{
    /// <summary>
    /// Drag handler on handmade inventory items. Forwards to <see cref="InventoryDisplayUI"/>.
    /// </summary>
    [RequireComponent(typeof(InventoryItemUI))]
    [RequireComponent(typeof(Image))]
    public sealed class InventoryDragController :
        MonoBehaviour,
        IBeginDragHandler,
        IDragHandler,
        IEndDragHandler,
        IInitializePotentialDragHandler
    {
        private InventoryItemUI _itemUi;
        private InventoryDisplayUI _display;
        private CanvasGroup _canvasGroup;

        private void Awake()
        {
            _itemUi = GetComponent<InventoryItemUI>();
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();

            var image = GetComponent<Image>();
            if (image != null)
                image.raycastTarget = true;

            ResolveDisplay();
        }

        private void ResolveDisplay()
        {
            _display = GetComponentInParent<InventoryDisplayUI>();
            if (_display == null)
                _display = InventoryDisplayUI.Active;
        }

        public void OnInitializePotentialDrag(PointerEventData eventData)
        {
            eventData.useDragThreshold = false;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            ResolveDisplay();
            if (_canvasGroup != null)
            {
                _canvasGroup.blocksRaycasts = false;
                _canvasGroup.alpha = 0.9f;
            }

            _display?.BeginDrag(_itemUi, eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            _display?.Drag(_itemUi, eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (_canvasGroup != null)
            {
                _canvasGroup.blocksRaycasts = true;
                _canvasGroup.alpha = 1f;
            }

            _display?.EndDrag(_itemUi, eventData);
        }
    }
}
