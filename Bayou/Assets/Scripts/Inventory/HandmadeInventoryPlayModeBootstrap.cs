#if UNITY_EDITOR
using Bayou.Inventory.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Bayou.Inventory
{
    /// <summary>
    /// Editor Play Mode only: ensure the brown MockUI UIGrid exists and strip procedural InventoryCanvas.
    /// </summary>
    public static class HandmadeInventoryPlayModeBootstrap
    {
        private const string UiGridPath = "Assets/MockUI/UIGrid.prefab";
        private const string CellPath = "Assets/MockUI/Cell.prefab";
        private const string ItemPath = "Assets/MockUI/InventoryItem.prefab";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Ensure()
        {
            if (!Application.isPlaying) return;

            StripProceduralCanvases();

            if (Object.FindFirstObjectByType<InventoryDisplayUI>() != null)
                return;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(UiGridPath);
            if (prefab == null)
            {
                Debug.LogWarning("[Bayou] MockUI/UIGrid.prefab missing — cannot spawn handmade bag.");
                return;
            }

            var instance = Object.Instantiate(prefab);
            instance.name = "UIGrid";

            var inventoryPanel = FindDeep(instance.transform, "Inventory") ?? instance.transform;
            var grid = FindDeep(inventoryPanel, "Grid");

            var display = inventoryPanel.GetComponent<InventoryDisplayUI>() ??
                          inventoryPanel.gameObject.AddComponent<InventoryDisplayUI>();

            InventoryGridUI gridUi = null;
            if (grid != null)
            {
                if (grid.GetComponent<GridLayoutGroup>() == null)
                    grid.gameObject.AddComponent<GridLayoutGroup>();
                gridUi = grid.GetComponent<InventoryGridUI>() ?? grid.gameObject.AddComponent<InventoryGridUI>();

                var cellGo = AssetDatabase.LoadAssetAtPath<GameObject>(CellPath);
                var cellUi = cellGo != null ? cellGo.GetComponent<InventoryCellUI>() : null;
                var so = new SerializedObject(gridUi);
                if (cellUi != null)
                    so.FindProperty("cellPrefab").objectReferenceValue = cellUi;
                so.FindProperty("columns").intValue = 7;
                so.FindProperty("rows").intValue = 6;
                so.FindProperty("fillParent").boolValue = true;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            var itemLayer = inventoryPanel.Find("ItemLayer");
            if (itemLayer == null)
            {
                var layerGo = new GameObject("ItemLayer", typeof(RectTransform));
                itemLayer = layerGo.transform;
                itemLayer.SetParent(inventoryPanel, false);
                var rt = (RectTransform)itemLayer;
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }

            var itemGo = AssetDatabase.LoadAssetAtPath<GameObject>(ItemPath);
            var itemUi = itemGo != null ? itemGo.GetComponent<InventoryItemUI>() : null;

            var inv = InventoryController.Instance ?? Object.FindFirstObjectByType<InventoryController>();
            var dso = new SerializedObject(display);
            if (inv != null)
                dso.FindProperty("inventory").objectReferenceValue = inv;
            if (gridUi != null)
                dso.FindProperty("gridUI").objectReferenceValue = gridUi;
            dso.FindProperty("panelRoot").objectReferenceValue = inventoryPanel;
            dso.FindProperty("itemLayer").objectReferenceValue = itemLayer.GetComponent<RectTransform>();
            if (itemUi != null)
                dso.FindProperty("itemPrefab").objectReferenceValue = itemUi;
            dso.ApplyModifiedPropertiesWithoutUndo();

            if (inv != null && gridUi != null)
            {
                var invSo = new SerializedObject(inv);
                invSo.FindProperty("backpackLayout").objectReferenceValue = null;
                invSo.FindProperty("gridWidth").intValue = 7;
                invSo.FindProperty("gridHeight").intValue = 6;
                invSo.ApplyModifiedPropertiesWithoutUndo();
            }

            var canvas = instance.GetComponent<Canvas>();
            if (canvas != null)
                canvas.sortingOrder = 15;

            Debug.Log("[Bayou] Spawned brown MockUI UIGrid for Play Mode.");
        }

        private static void StripProceduralCanvases()
        {
            foreach (var ui in Object.FindObjectsByType<InventoryUIController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (ui == null) continue;
                if (ui.GetComponentInParent<InventoryDisplayUI>() != null) continue;
                Object.Destroy(ui.gameObject);
            }
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            foreach (Transform child in root)
            {
                var hit = FindDeep(child, name);
                if (hit != null) return hit;
            }

            return null;
        }
    }
}
#endif
