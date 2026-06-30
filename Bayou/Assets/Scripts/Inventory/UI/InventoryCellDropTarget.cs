using UnityEngine;
using UnityEngine.EventSystems;

namespace Bayou.Inventory.UI
{
    /// <summary>
    /// Optional: drop on a specific cell (drag end still uses mouse position; this can be extended).
    /// </summary>
    public sealed class InventoryCellDropTarget : MonoBehaviour, IDropHandler
    {
        private InventoryUIController _ui;
        private int _x;
        private int _y;

        public void Init(InventoryUIController ui, int x, int y)
        {
            _ui = ui;
            _x = x;
            _y = y;
        }

        public void OnDrop(PointerEventData eventData)
        {
            // Placement handled in InventoryItemView.EndDrag via screen position.
        }
    }
}
