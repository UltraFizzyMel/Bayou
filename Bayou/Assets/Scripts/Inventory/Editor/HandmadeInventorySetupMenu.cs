#if UNITY_EDITOR
using Bayou.Inventory;
using Bayou.Inventory.Shop;
using Bayou.Testing;
using Bayou.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace Bayou.Inventory.Editor
{
    /// <summary>
    /// Wires InventoryTest (handmade MockUI grid) without replacing it with the procedural inventory UI.
    /// </summary>
    public static class HandmadeInventorySetupMenu
    {
        [MenuItem("Bayou/Inventory/Fix Handmade Inventory Test Scene", false, 5)]
        public static void FixHandmadeInventoryTestScene()
        {
            EnsureEventSystem();
            EnsurePlayerWallet();
            WireHandmadeDisplay();
            ShopSetupMenu.SetupShopInScene(forceRecreate: true);
            WireShopToHandmade();
            EnsurePlaytestHarnessRefs();

            var uiModule = Object.FindFirstObjectByType<InputSystemUIInputModule>();
            if (uiModule != null)
                BayouUiInputBootstrap.Wire(uiModule, null);

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log(
                "[Bayou] Handmade inventory test scene fixed.\n" +
                "  I = toggle bag  |  drag + R = rotate  |  Shift+5 = shop  |  Shift+1 = add fish");
        }

        private static void EnsureEventSystem()
        {
            var es = Object.FindFirstObjectByType<EventSystem>();
            if (es == null)
            {
                var go = new GameObject("EventSystem");
                Undo.RegisterCreatedObjectUndo(go, "Create EventSystem");
                es = go.AddComponent<EventSystem>();
                go.AddComponent<InputSystemUIInputModule>();
            }
            else if (es.GetComponent<InputSystemUIInputModule>() == null &&
                     es.GetComponent<StandaloneInputModule>() == null)
            {
                Undo.AddComponent<InputSystemUIInputModule>(es.gameObject);
            }
        }

        private static void EnsurePlayerWallet()
        {
            if (Object.FindFirstObjectByType<PlayerWallet>() != null)
                return;

            var inv = Object.FindFirstObjectByType<InventoryController>();
            var host = inv != null ? inv.gameObject : new GameObject("PlayerWallet");
            if (inv == null)
                Undo.RegisterCreatedObjectUndo(host, "Create PlayerWallet");

            if (host.GetComponent<PlayerWallet>() == null)
                Undo.AddComponent<PlayerWallet>(host);
        }

        private static void WireHandmadeDisplay()
        {
            var display = Object.FindFirstObjectByType<InventoryDisplayUI>();
            if (display == null)
            {
                Debug.LogWarning("[Bayou] No InventoryDisplayUI in scene — add it to your Inventory panel.");
                return;
            }

            var so = new SerializedObject(display);
            var inv = Object.FindFirstObjectByType<InventoryController>();
            var grid = Object.FindFirstObjectByType<InventoryGridUI>();
            var itemPrefab = Object.FindFirstObjectByType<InventoryItemUI>(FindObjectsInactive.Include);

            if (so.FindProperty("inventory").objectReferenceValue == null && inv != null)
                so.FindProperty("inventory").objectReferenceValue = inv;
            if (so.FindProperty("gridUI").objectReferenceValue == null && grid != null)
                so.FindProperty("gridUI").objectReferenceValue = grid;
            if (so.FindProperty("panelRoot").objectReferenceValue == null)
                so.FindProperty("panelRoot").objectReferenceValue = display.transform;
            if (so.FindProperty("itemLayer").objectReferenceValue == null)
            {
                var layer = GameObject.Find("ItemLayer");
                if (layer != null)
                    so.FindProperty("itemLayer").objectReferenceValue = layer.GetComponent<RectTransform>();
            }
            // Prefer the MockUI prefab asset (stable) over a fragile scene-instance reference.
            var itemPrefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/MockUI/InventoryItem.prefab");
            var itemUiOnAsset = itemPrefabAsset != null ? itemPrefabAsset.GetComponent<InventoryItemUI>() : null;
            if (itemUiOnAsset != null)
                so.FindProperty("itemPrefab").objectReferenceValue = itemUiOnAsset;
            else if (so.FindProperty("itemPrefab").objectReferenceValue == null && itemPrefab != null)
                so.FindProperty("itemPrefab").objectReferenceValue = itemPrefab;

            so.ApplyModifiedPropertiesWithoutUndo();

            if (itemPrefab != null)
                itemPrefab.gameObject.SetActive(false);

            // Match controller grid size to handmade UI.
            if (inv != null && grid != null)
            {
                var invSo = new SerializedObject(inv);
                invSo.FindProperty("gridWidth").intValue = grid.Columns;
                invSo.FindProperty("gridHeight").intValue = grid.Rows;
                invSo.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void WireShopToHandmade()
        {
            var shop = Object.FindFirstObjectByType<ShopUIController>();
            var display = Object.FindFirstObjectByType<InventoryDisplayUI>();
            if (shop == null || display == null) return;

            var so = new SerializedObject(shop);
            so.FindProperty("handmadePlayerInventoryUi").objectReferenceValue = display;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsurePlaytestHarnessRefs()
        {
            var harness = Object.FindFirstObjectByType<PlaytestHarness>();
            if (harness == null) return;

            var fish = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Inventory/Items/Item_Fish.asset");
            var shopDef = AssetDatabase.LoadAssetAtPath<ShopDefinition>("Assets/Inventory/Shop/Shop_BayouMerchant.asset");
            var so = new SerializedObject(harness);
            if (so.FindProperty("testFishItem").objectReferenceValue == null && fish != null)
                so.FindProperty("testFishItem").objectReferenceValue = fish;
            if (so.FindProperty("testShop").objectReferenceValue == null && shopDef != null)
                so.FindProperty("testShop").objectReferenceValue = shopDef;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
