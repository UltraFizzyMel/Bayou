using UnityEngine.EventSystems;

namespace Bayou.Inventory.UI
{
    public interface IInventoryDragHost
    {
        void BeginDrag(InventoryItemView view);
        void Dragging(InventoryItemView view, PointerEventData eventData);
        void EndDrag(InventoryItemView view, PointerEventData eventData);
    }
}
