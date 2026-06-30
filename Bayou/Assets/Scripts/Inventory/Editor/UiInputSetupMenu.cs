#if UNITY_EDITOR
using Bayou.Inventory.Shop;
using Bayou.Inventory.UI;
using Bayou.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace Bayou.Inventory.Editor
{
    public static class UiInputSetupMenu
    {
        [MenuItem("Bayou/UI/Fix UI Input (Inventory Clicks & Drag)", false, 10)]
        public static void FixUiInputInScene()
        {
            var eventSystem = Object.FindFirstObjectByType<EventSystem>();
            if (eventSystem == null)
            {
                var esGo = new GameObject("EventSystem");
                eventSystem = esGo.AddComponent<EventSystem>();
                Undo.RegisterCreatedObjectUndo(esGo, "Create EventSystem");
            }

            var module = eventSystem.GetComponent<InputSystemUIInputModule>();
            if (module == null)
                module = Undo.AddComponent<InputSystemUIInputModule>(eventSystem.gameObject);

            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
            if (actions == null)
            {
                Debug.LogError("[Bayou] Could not find Assets/InputSystem_Actions.inputactions");
                return;
            }

            var moduleSo = new SerializedObject(module);
            moduleSo.FindProperty("m_ActionsAsset").objectReferenceValue = actions;
            moduleSo.ApplyModifiedPropertiesWithoutUndo();

            var bootstrap = eventSystem.GetComponent<BayouUiInputBootstrap>();
            if (bootstrap == null)
                bootstrap = Undo.AddComponent<BayouUiInputBootstrap>(eventSystem.gameObject);

            var bootstrapSo = new SerializedObject(bootstrap);
            bootstrapSo.FindProperty("actionsAsset").objectReferenceValue = actions;
            bootstrapSo.ApplyModifiedPropertiesWithoutUndo();

            Selection.activeGameObject = eventSystem.gameObject;
            InventoryInputWiring.WireInventoryActionsInScene();
            Debug.Log("[Bayou] UI input fixed. Point/Click actions wired for inventory drag-and-drop.");
        }
    }
}
#endif
