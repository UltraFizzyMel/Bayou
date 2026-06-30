using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Bayou.Inventory.UI
{
    internal static class InventoryDragInput
    {
        public static bool WasRotatePressedThisFrame(UnityEngine.InputSystem.InputActionReference rotateAction)
        {
#if ENABLE_INPUT_SYSTEM
            if (rotateAction?.action != null)
            {
                if (!rotateAction.action.enabled)
                    rotateAction.action.Enable();

                if (rotateAction.action.WasPressedThisFrame())
                    return true;
            }

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame)
                return true;
#endif
            return false;
        }
    }
}
