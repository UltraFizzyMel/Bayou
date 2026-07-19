using UnityEngine;

namespace Bayou.Inventory.UI
{
    /// <summary>Shared 1×1 white sprite so UI Images without a sprite still draw and raycast.</summary>
    internal static class UiWhiteSprite
    {
        private static Sprite _sprite;

        public static Sprite Get()
        {
            if (_sprite != null) return _sprite;
            var tex = Texture2D.whiteTexture;
            _sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            _sprite.name = "BayouUiWhite";
            return _sprite;
        }
    }
}
