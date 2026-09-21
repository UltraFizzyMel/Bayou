using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Bayou.Inventory.UI
{
    /// <summary>
    /// Draws an item plate from occupied cells. A solid rectangle stays one image.
    /// A partial footprint (catfish) no longer paints the empty cells as a square.
    /// </summary>
    public static class ItemShapePlate
    {
        private const string CellName = "ShapeCell";

        public static void Apply(RectTransform host, Image plate, ItemShape shape, int rotation, Color plateColor)
        {
            if (host == null || plate == null) return;

            shape.EnsureValid();

            // Root image stays a raycast target. The visible plate is inset so it
            // doesn't sit flush against the backpack frame or the empty cells.
            var ghost = plateColor;
            ghost.a = 0.02f;
            plate.color = ghost;
            plate.raycastTarget = true;

            var offsets = new List<Vector2Int>(8);
            shape.GetOccupiedOffsets(rotation, offsets);
            if (offsets.Count == 0)
            {
                plate.color = plateColor;
                return;
            }

            var minX = offsets[0].x;
            var maxX = offsets[0].x;
            var minY = offsets[0].y;
            var maxY = offsets[0].y;
            for (var i = 1; i < offsets.Count; i++)
            {
                var o = offsets[i];
                if (o.x < minX) minX = o.x;
                if (o.x > maxX) maxX = o.x;
                if (o.y < minY) minY = o.y;
                if (o.y > maxY) maxY = o.y;
            }

            var cols = Mathf.Max(1, maxX - minX + 1);
            var rows = Mathf.Max(1, maxY - minY + 1);
            var size = host.rect.size;
            if (size.x < 2f || size.y < 2f)
                size = host.sizeDelta;
            var cellW = size.x / cols;
            var cellH = size.y / rows;
            var inset = Mathf.Clamp(Mathf.Min(cellW, cellH) * 0.1f, 3f, 7f);

            var keep = 0;
            if (IsSolid(shape))
            {
                var cell = GetOrCreateCell(host, 0);
                keep = 1;
                PlaceCell(cell, inset, -inset, size.x - inset * 2f, size.y - inset * 2f, plateColor);
                cell.rectTransform.SetSiblingIndex(0);
            }
            else
            {
                for (var i = 0; i < offsets.Count; i++)
                {
                    var o = offsets[i];
                    var left = Contains(offsets, o.x - 1, o.y) ? 0f : inset;
                    var right = Contains(offsets, o.x + 1, o.y) ? 0f : inset;
                    var top = Contains(offsets, o.x, o.y - 1) ? 0f : inset;
                    var bottom = Contains(offsets, o.x, o.y + 1) ? 0f : inset;
                    var cell = GetOrCreateCell(host, keep);
                    keep++;
                    var x = (o.x - minX) * cellW + left;
                    var y = -((o.y - minY) * cellH + top);
                    PlaceCell(cell, x, y, cellW - left - right, cellH - top - bottom, plateColor);
                    cell.rectTransform.SetSiblingIndex(i);
                }
            }

            var seen = 0;
            for (var i = 0; i < host.childCount; i++)
            {
                var child = host.GetChild(i);
                if (child.name != CellName) continue;
                seen++;
                if (seen > keep)
                    child.gameObject.SetActive(false);
                else
                    child.gameObject.SetActive(true);
            }
        }

        private static void PlaceCell(Image cell, float x, float y, float w, float h, Color plateColor)
        {
            var rt = cell.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(Mathf.Max(1f, w), Mathf.Max(1f, h));
            rt.anchoredPosition = new Vector2(x, y);
            cell.color = plateColor;
            cell.raycastTarget = false;
        }

        private static bool Contains(List<Vector2Int> offsets, int x, int y)
        {
            for (var i = 0; i < offsets.Count; i++)
            {
                if (offsets[i].x == x && offsets[i].y == y)
                    return true;
            }

            return false;
        }

        private static bool IsSolid(ItemShape shape)
        {
            var needed = Mathf.Max(1, shape.width) * Mathf.Max(1, shape.height);
            return shape.OccupiedCellCount >= needed;
        }

        private static Image GetOrCreateCell(RectTransform host, int index)
        {
            var seen = 0;
            for (var i = 0; i < host.childCount; i++)
            {
                var child = host.GetChild(i);
                if (child.name != CellName) continue;
                if (seen == index)
                {
                    child.gameObject.SetActive(true);
                    return child.GetComponent<Image>();
                }

                seen++;
            }

            var go = new GameObject(CellName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(host, false);
            var image = go.GetComponent<Image>();
            image.sprite = UiWhiteSprite.Get();
            image.type = Image.Type.Simple;
            return image;
        }
    }
}
