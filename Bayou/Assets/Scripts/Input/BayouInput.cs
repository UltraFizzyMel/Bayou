using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Bayou.Input
{
    public static class BayouInput
    {
        public static Vector2 ReadMove(
#if ENABLE_INPUT_SYSTEM
            InputActionReference moveAction
#else
            object _
#endif
        )
        {
#if ENABLE_INPUT_SYSTEM
            if (moveAction != null && moveAction.action != null)
            {
                try
                {
                    if (!moveAction.action.enabled)
                        moveAction.action.Enable();
                    return moveAction.action.ReadValue<Vector2>();
                }
                catch
                {
                    // fall through
                }
            }

            // New Input System only projects: old Input.GetAxisRaw is often dead.
            var kb = Keyboard.current;
            if (kb != null)
            {
                var x = 0f;
                var y = 0f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) x -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) x += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) y -= 1f;
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) y += 1f;
                if (x != 0f || y != 0f)
                    return new Vector2(x, y).normalized;
            }

            var pad = Gamepad.current;
            if (pad != null)
            {
                var stick = pad.leftStick.ReadValue();
                if (stick.sqrMagnitude > 0.01f)
                    return stick;
            }

            return Vector2.zero;
#else
            return new Vector2(UnityEngine.Input.GetAxisRaw("Horizontal"), UnityEngine.Input.GetAxisRaw("Vertical"));
#endif
        }
    }
}
