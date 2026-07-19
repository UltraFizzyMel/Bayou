using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Bayou.UI
{
    /// <summary>
    /// Ensures <see cref="InputSystemUIInputModule"/> has Point / Click actions wired so uGUI drag-and-drop works.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-1000)]
    public sealed class BayouUiInputBootstrap : MonoBehaviour
    {
        private const string DefaultActionsPath = "Assets/InputSystem_Actions.inputactions";

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

            // InventoryTest often has an EventSystem with no bootstrap / no actions asset.
            EnsureBootstrapComponent();
        }

        private static void EnsureBootstrapComponent()
        {
            var es = Object.FindFirstObjectByType<EventSystem>();
            if (es == null) return;
            if (es.GetComponent<BayouUiInputBootstrap>() == null)
                es.gameObject.AddComponent<BayouUiInputBootstrap>();
        }

        public static void Wire(InputSystemUIInputModule module, InputActionAsset fallbackAsset = null)
        {
            if (module == null) return;

            var asset = module.actionsAsset != null ? module.actionsAsset : fallbackAsset;
            if (asset == null)
                asset = LoadDefaultActionsAsset();

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
                // Player Controls.inputactions has no UI map — fall back to the default UI asset.
                var uiAsset = LoadDefaultActionsAsset();
                if (uiAsset != null && uiAsset != asset)
                {
                    module.actionsAsset = uiAsset;
                    asset = uiAsset;
                    uiMap = asset.FindActionMap("UI", throwIfNotFound: false);
                }
            }

            if (uiMap == null)
            {
                Debug.LogWarning("[Bayou UI] InputActionAsset is missing a 'UI' action map.");
                return;
            }

            // Always re-bind — scene YAML often serializes Point/Click as {fileID:0}.
            var point = CreateRef(uiMap, "Point");
            var click = CreateRef(uiMap, "Click");
            if (point != null) module.point = point;
            if (click != null) module.leftClick = click;

            var right = CreateRef(uiMap, "RightClick");
            if (right != null) module.rightClick = right;
            var middle = CreateRef(uiMap, "MiddleClick");
            if (middle != null) module.middleClick = middle;
            var scroll = CreateRef(uiMap, "ScrollWheel");
            if (scroll != null) module.scrollWheel = scroll;
            var move = CreateRef(uiMap, "Navigate");
            if (move != null) module.move = move;
            var submit = CreateRef(uiMap, "Submit");
            if (submit != null) module.submit = submit;
            var cancel = CreateRef(uiMap, "Cancel");
            if (cancel != null) module.cancel = cancel;

            if (!uiMap.enabled)
                uiMap.Enable();
        }

        private static InputActionAsset LoadDefaultActionsAsset()
        {
#if UNITY_EDITOR
            var fromEditor = AssetDatabase.LoadAssetAtPath<InputActionAsset>(DefaultActionsPath);
            if (fromEditor != null)
                return fromEditor;
#endif
            // Prefer any loaded asset that already has a UI map.
            foreach (var asset in Resources.FindObjectsOfTypeAll<InputActionAsset>())
            {
                if (asset != null && asset.FindActionMap("UI", throwIfNotFound: false) != null)
                    return asset;
            }

            return null;
        }

        private static InputActionReference CreateRef(InputActionMap map, string actionName)
        {
            var action = map.FindAction(actionName, throwIfNotFound: false);
            return action != null ? InputActionReference.Create(action) : null;
        }
    }
}
