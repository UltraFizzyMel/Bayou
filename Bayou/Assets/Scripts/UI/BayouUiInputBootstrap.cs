using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace Bayou.UI
{
    /// <summary>
    /// Ensures <see cref="InputSystemUIInputModule"/> has Point / Click actions wired so uGUI drag-and-drop works.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-1000)]
    public sealed class BayouUiInputBootstrap : MonoBehaviour
    {
        [SerializeField] private InputActionAsset actionsAsset;

        private void Awake()
        {
            var module = GetComponent<InputSystemUIInputModule>();
            if (module == null)
                module = FindFirstObjectByType<InputSystemUIInputModule>();

            Wire(module, actionsAsset);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void WireSceneModuleOnLoad()
        {
            var module = Object.FindFirstObjectByType<InputSystemUIInputModule>();
            if (module != null)
                Wire(module, null);
        }

        public static void Wire(InputSystemUIInputModule module, InputActionAsset fallbackAsset = null)
        {
            if (module == null) return;

            var asset = module.actionsAsset != null ? module.actionsAsset : fallbackAsset;
            if (asset == null)
            {
                Debug.LogWarning("[Bayou UI] No InputActionAsset for UI input. Mouse will not interact with inventory.");
                return;
            }

            if (module.actionsAsset == null)
                module.actionsAsset = asset;

            var uiMap = asset.FindActionMap("UI", throwIfNotFound: false);
            if (uiMap == null)
            {
                Debug.LogWarning("[Bayou UI] InputActionAsset is missing a 'UI' action map.");
                return;
            }

            if (module.point == null)
                module.point = CreateRef(uiMap, "Point");
            if (module.leftClick == null)
                module.leftClick = CreateRef(uiMap, "Click");
            if (module.rightClick == null)
                module.rightClick = CreateRef(uiMap, "RightClick");
            if (module.middleClick == null)
                module.middleClick = CreateRef(uiMap, "MiddleClick");
            if (module.scrollWheel == null)
                module.scrollWheel = CreateRef(uiMap, "ScrollWheel");
            if (module.move == null)
                module.move = CreateRef(uiMap, "Navigate");
            if (module.submit == null)
                module.submit = CreateRef(uiMap, "Submit");
            if (module.cancel == null)
                module.cancel = CreateRef(uiMap, "Cancel");

            if (!uiMap.enabled)
                uiMap.Enable();
        }

        private static InputActionReference CreateRef(InputActionMap map, string actionName)
        {
            var action = map.FindAction(actionName, throwIfNotFound: false);
            return action != null ? InputActionReference.Create(action) : null;
        }
    }
}
