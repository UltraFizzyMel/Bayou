using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Bayou.Inventory.UI
{
    /// <summary>
    /// Top-most overlay canvas so dragged inventory items are not clipped by bag masks
    /// or hidden under the other shop panel.
    /// </summary>
    public static class InventoryDragOverlay
    {
        private const string RootName = "InventoryDragOverlay";
        private const int SortOrder = 200;

        private static Canvas _canvas;
        private static RectTransform _root;

        public static RectTransform Root
        {
            get
            {
                Ensure();
                return _root;
            }
        }

        public static void Attach(RectTransform item)
        {
            if (item == null) return;
            Ensure();
            SetMaskable(item, false);
            item.SetParent(_root, worldPositionStays: true);
            item.SetAsLastSibling();
            item.localScale = Vector3.one;
            item.localRotation = Quaternion.identity;
        }

        public static void Follow(RectTransform item, PointerEventData eventData)
        {
            if (item == null) return;
            Ensure();

            if (item.parent != _root)
                Attach(item);

            var cam = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? _canvas.worldCamera
                : null;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _root, eventData.position, cam, out var local))
                return;

            // Center the item on the cursor so it tracks across both shop panels.
            item.pivot = new Vector2(0.5f, 0.5f);
            item.anchorMin = item.anchorMax = new Vector2(0.5f, 0.5f);
            item.anchoredPosition = local;
        }

        public static void SetMaskable(RectTransform item, bool maskable)
        {
            if (item == null) return;
            var graphics = item.GetComponentsInChildren<MaskableGraphic>(true);
            for (var i = 0; i < graphics.Length; i++)
            {
                if (graphics[i] != null)
                    graphics[i].maskable = maskable;
            }
        }

        private static void Ensure()
        {
            if (_root != null && _canvas != null) return;

            var existing = GameObject.Find(RootName);
            if (existing != null)
            {
                _canvas = existing.GetComponent<Canvas>();
                _root = existing.transform as RectTransform;
                if (_root != null && _canvas != null) return;
            }

            var go = new GameObject(RootName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            Object.DontDestroyOnLoad(go);

            _root = go.GetComponent<RectTransform>();
            _root.anchorMin = Vector2.zero;
            _root.anchorMax = Vector2.one;
            _root.offsetMin = Vector2.zero;
            _root.offsetMax = Vector2.zero;

            _canvas = go.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.overrideSorting = true;
            _canvas.sortingOrder = SortOrder;
            // No GraphicRaycaster — the ghost must never steal drops.

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }
    }
}
