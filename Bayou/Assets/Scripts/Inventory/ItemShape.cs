using System;
using System.Collections.Generic;
using UnityEngine;

namespace Bayou.Inventory
{
    /// <summary>
    /// Tetris-style footprint: true cells are occupied. Origin is top-left of the bounding box.
    /// </summary>
    [Serializable]
    public struct ItemShape
    {
        public int width;
        public int height;
        [Tooltip("Row-major: index = y * width + x")]
        public bool[] cells;

        public bool IsOccupied(int x, int y)
        {
            if (x < 0 || y < 0 || x >= width || y >= height) return false;
            var i = y * width + x;
            return cells != null && i < cells.Length && cells[i];
        }

        public int OccupiedCellCount
        {
            get
            {
                if (cells == null) return 0;
                var n = 0;
                foreach (var c in cells)
                    if (c) n++;
                return n;
            }
        }

        /// <summary>Offsets (dx, dy) from anchor for a given rotation (0–3 = 0°, 90°, 180°, 270° CW).</summary>
        public void GetOccupiedOffsets(int rotation, List<Vector2Int> into)
        {
            into.Clear();
            rotation = ((rotation % 4) + 4) % 4;
            var w = width;
            var h = height;

            for (var y = 0; y < h; y++)
            {
                for (var x = 0; x < w; x++)
                {
                    if (!IsOccupied(x, y)) continue;
                    var (rx, ry) = RotateCell(x, y, w, h, rotation);
                    into.Add(new Vector2Int(rx, ry));
                }
            }
        }

        public void GetBounds(int rotation, out int boundW, out int boundH)
        {
            rotation = ((rotation % 4) + 4) % 4;
            if (rotation % 2 == 0)
            {
                boundW = width;
                boundH = height;
            }
            else
            {
                boundW = height;
                boundH = width;
            }
        }

        private static (int x, int y) RotateCell(int x, int y, int w, int h, int rotation)
        {
            return rotation switch
            {
                0 => (x, y),
                1 => (h - 1 - y, x),
                2 => (w - 1 - x, h - 1 - y),
                3 => (y, w - 1 - x),
                _ => (x, y)
            };
        }

        public static ItemShape Rectangle(int w, int h)
        {
            var cells = new bool[w * h];
            for (var i = 0; i < cells.Length; i++)
                cells[i] = true;
            return new ItemShape { width = w, height = h, cells = cells };
        }

        public static ItemShape LShape()
        {
            // 2x2 L: XX / X.
            return new ItemShape
            {
                width = 2,
                height = 2,
                cells = new[] { true, true, true, false }
            };
        }
    }
}
