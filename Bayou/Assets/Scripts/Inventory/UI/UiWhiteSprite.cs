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
            // whiteTexture is not readable in a player. Sprite.Create on it faults
            // the first time the hand-net plate is built.
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
            tex.Apply(false, false);
            _sprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 100f);
            _sprite.name = "BayouUiWhite";
            return _sprite;
        }
    }
}
