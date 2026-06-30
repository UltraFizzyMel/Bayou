#if UNITY_EDITOR
using Bayou.Inventory.Shop;
using Bayou.Inventory.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Bayou.Inventory.Editor
{
    internal static class InventoryInputWiring
    {
        private const string ControlsPath = "Assets/InputManagement/Controls.inputactions";

        public static void WireInventoryActionsInScene()
        {
            var controls = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ControlsPath);
            if (controls == null)
            {
                Debug.LogWarning("[Bayou] Controls.inputactions not found — rotate falls back to R key at runtime.");
                return;
            }

            var toggle = CreateRef(controls, "Toggle Inventory");
            var rotate = CreateRef(controls, "Rotate");

            foreach (var ui in Object.FindObjectsByType<InventoryUIController>(FindObjectsSortMode.None))
            {
                var so = new SerializedObject(ui);
                if (so.FindProperty("toggleInventoryAction").objectReferenceValue == null)
                    so.FindProperty("toggleInventoryAction").objectReferenceValue = toggle;
                if (so.FindProperty("rotateItemAction").objectReferenceValue == null)
                    so.FindProperty("rotateItemAction").objectReferenceValue = rotate;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            foreach (var shop in Object.FindObjectsByType<ShopUIController>(FindObjectsSortMode.None))
            {
                var so = new SerializedObject(shop);
                if (so.FindProperty("rotateItemAction").objectReferenceValue == null)
                    so.FindProperty("rotateItemAction").objectReferenceValue = rotate;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static InputActionReference CreateRef(InputActionAsset asset, string actionName)
        {
            var action = asset.FindAction(actionName, throwIfNotFound: false);
            return action != null ? InputActionReference.Create(action) : null;
        }
    }
}
#endif
