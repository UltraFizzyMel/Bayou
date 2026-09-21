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

        public static Vector2Int RotateGrabClockwise(ItemShape shape, int rotationBefore, Vector2Int grab)
        {
            shape.GetBounds(rotationBefore, out _, out var boundH);
            return new Vector2Int(boundH - 1 - grab.y, grab.x);
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

            // Adjacent-row off-by-one: keep the grabbed cell and nudge Y.
            if (canPlaceAt(anchorX, anchorY - 1))
            {
                anchorY -= 1;
                return true;
            }
            if (canPlaceAt(anchorX, anchorY + 1))
            {
                anchorY += 1;
                return true;
            }

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
