using UnityEngine;
using UnityEngine.UI;

namespace Bayou.Inventory
{
    [DisallowMultipleComponent]
    public sealed class InventoryCellUI : MonoBehaviour
    {
        public int X { get; private set; }
        public int Y { get; private set; }
        public RectTransform Rect { get; private set; }

        private Image _image;
        private Color _baseColor = InventoryGridUI.BackpackCellTint;

        private void Awake()
        {
            Rect = GetComponent<RectTransform>();
            _image = GetComponent<Image>();
            if (_image == null) return;

            // A null sprite still draws. Color must be the see-through black or the quad is white.
            _image.sprite = null;
            _image.type = Image.Type.Simple;
            _baseColor = InventoryGridUI.BackpackCellTint;
            _image.color = _baseColor;
        }

        public void Setup(int x, int y)
        {
            X = x;
            Y = y;
            name = $"Cell_{x}_{y}";
            if (Rect == null) Rect = GetComponent<RectTransform>();
            if (_image == null) _image = GetComponent<Image>();
        }

        public void SetBaseColor(Color color)
        {
            _baseColor = color;
            if (_image != null)
                _image.color = color;
        }

        public void SetHighlight(Color? color)
        {
            if (_image == null) return;
            _image.color = color ?? _baseColor;
        }

        public void ClearHighlight() => SetHighlight(null);
    }
}
