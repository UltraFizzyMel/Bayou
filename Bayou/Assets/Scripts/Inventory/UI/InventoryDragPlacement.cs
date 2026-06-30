using System.Collections.Generic;
using UnityEngine;

namespace Bayou.Inventory.UI
{
    /// <summary>
    /// Resolves grid anchor from the cell under the cursor (RE-style: the grabbed cell follows the mouse).
    /// </summary>
    internal static class InventoryDragPlacement
    {
        public static Vector2Int ComputeGrabOffset(
            ItemShape shape,
            int rotation,
            int anchorX,
            int anchorY,
            int hoverX,
            int hoverY)
        {
            var offsets = new List<Vector2Int>();
            shape.GetOccupiedOffsets(rotation, offsets);
            foreach (var o in offsets)
            {
                if (anchorX + o.x == hoverX && anchorY + o.y == hoverY)
                    return o;
            }

            return Vector2Int.zero;
        }

        public static bool TryGetAnchorFromHover(
            ItemShape shape,
            int rotation,
            int hoverX,
            int hoverY,
            Vector2Int grabOffset,
            System.Func<int, int, bool> canPlaceAt,
            out int anchorX,
            out int anchorY)
        {
            anchorX = hoverX - grabOffset.x;
            anchorY = hoverY - grabOffset.y;
            if (canPlaceAt(anchorX, anchorY))
                return true;

            if (canPlaceAt(hoverX, hoverY))
            {
                anchorX = hoverX;
                anchorY = hoverY;
                return true;
            }

            var offsets = new List<Vector2Int>();
            shape.GetOccupiedOffsets(rotation, offsets);
            foreach (var o in offsets)
            {
                var ax = hoverX - o.x;
                var ay = hoverY - o.y;
                if (!canPlaceAt(ax, ay)) continue;
                anchorX = ax;
                anchorY = ay;
                return true;
            }

            anchorX = hoverX - grabOffset.x;
            anchorY = hoverY - grabOffset.y;
            return false;
        }
    }
}
