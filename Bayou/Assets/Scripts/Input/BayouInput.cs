using UnityEngine;

namespace Bayou.Input
{
    public static class BayouInput
    {
        public static Vector2 ReadMove(
#if ENABLE_INPUT_SYSTEM
            UnityEngine.InputSystem.InputActionReference moveAction
#else
            object _
#endif
        )
        {
#if ENABLE_INPUT_SYSTEM
            if (moveAction != null && moveAction.action != null)
            {
                try { return moveAction.action.ReadValue<Vector2>(); }
                catch { return Vector2.zero; }
            }
#endif
            return new Vector2(UnityEngine.Input.GetAxisRaw("Horizontal"), UnityEngine.Input.GetAxisRaw("Vertical"));
        }
    }
}

