#if UNITY_EDITOR
using Bayou.Inventory;
using Bayou.Inventory.Shop;
using Bayou.Inventory.UI;
using Bayou.Player;
using Bayou.Testing;
using Bayou.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Bayou.Inventory.Editor
{
    /// <summary>
    /// Wires the handmade MockUI inventory (InventoryTest style) — not the procedural InventoryUIController.
    /// </summary>
    public static class HandmadeInventorySetupMenu
    {
        private const string UiGridPrefabPath = "Assets/MockUI/UIGrid.prefab";
        private const string CellPrefabPath = "Assets/MockUI/Cell.prefab";
        private const string ItemPrefabPath = "Assets/MockUI/InventoryItem.prefab";

        [MenuItem("Bayou/Inventory/Fix Handmade Inventory Test Scene", false, 5)]
        public static void FixHandmadeInventoryTestScene()
        {
            EnsureHandmadeInventoryInScene(removeProceduralInventory: true);
            ShopSetupMenu.SetupShopInScene(forceRecreate: true);
            WireShopToHandmade();
            EnsurePlaytestHarnessRefs();
            InventoryInputWiring.WireInventoryActionsInScene();

            var uiModule = Object.FindFirstObjectByType<InputSystemUIInputModule>();
            if (uiModule != null)
                BayouUiInputBootstrap.Wire(uiModule, null);

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log(
                "[Bayou] Handmade MockUI inventory ready.\n" +
                "  I = toggle bag  |  drag + R = rotate  |  Shift+5 = shop  |  Shift+1 = add fish");
        }

        [MenuItem("Bayou/Inventory/Ensure Handmade Inventory In Open Scene", false, 6)]
        public static void EnsureHandmadeInventoryInOpenSceneMenu()
        {
            EnsureHandmadeInventoryInScene(removeProceduralInventory: true);
            InventoryInputWiring.WireInventoryActionsInScene();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[Bayou] Handmade MockUI UIGrid ensured (procedural InventoryCanvas removed).");
        }

        [MenuItem("Bayou/Inventory/Restore Brown UIGrid In MovementTest", false, 7)]
        public static void RestoreBrownUiGridInMovementTest()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            var scene = EditorSceneManager.OpenScene("Assets/Scenes/MovementTest.unity", OpenSceneMode.Single);
            EnsureHandmadeInventoryInScene(removeProceduralInventory: true);
            WireShopToHandmade();
            InventoryInputWiring.WireInventoryActionsInScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Bayou] MovementTest now uses your brown MockUI/UIGrid (same as InventoryTest). Procedural InventoryCanvas removed.");
        }

        /// <summary>
        /// Spawns/wires MockUI bag + InventoryController. Safe to call from MovementTest playtest setup.
        /// </summary>
        public static InventoryDisplayUI EnsureHandmadeInventoryInScene(bool removeProceduralInventory)
        {
            InventorySetupMenu.CreateSampleItems();

            if (removeProceduralInventory)
                RemoveProceduralInventoryUi();

            EnsureEventSystem();
            var player = ResolvePlayer();
            TagAsPlayer(player);
            var inventory = EnsureInventoryController(player);
            EnsurePlayerWallet(player, inventory);

            var display = Object.FindFirstObjectByType<InventoryDisplayUI>();
            if (display == null)
                display = SpawnMockUiGrid();

            WireHandmadeDisplay(display, inventory);
            return display;
        }

        private static void RemoveProceduralInventoryUi()
        {
            // Nuke every code-built attaché bag — never touch MockUI UIGrid / InventoryDisplayUI.
            foreach (var ui in Object.FindObjectsByType<InventoryUIController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (ui == null) continue;
                if (ui.GetComponentInParent<InventoryDisplayUI>() != null) continue;
                Object.DestroyImmediate(ui.gameObject);
            }

            // Leftover canvases from failed setups (MovementTest had two).
            var roots = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var t in roots)
            {
                if (t == null || t.parent != null) continue;
                if (t.name != "InventoryCanvas") continue;
                if (t.GetComponentInChildren<InventoryDisplayUI>(true) != null) continue;
                Object.DestroyImmediate(t.gameObject);
            }
        }

        private static InventoryDisplayUI SpawnMockUiGrid()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(UiGridPrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[Bayou] Missing {UiGridPrefabPath}");
                return null;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Undo.RegisterCreatedObjectUndo(instance, "Spawn MockUI UIGrid");
            instance.name = "UIGrid";

            var inventoryPanel = FindChildByName(instance.transform, "Inventory");
            if (inventoryPanel == null)
                inventoryPanel = instance.transform;

            var grid = FindChildByName(inventoryPanel, "Grid");
            if (grid == null)
                grid = FindChildByName(instance.transform, "Grid");

            var display = inventoryPanel.GetComponent<InventoryDisplayUI>();
            if (display == null)
                display = Undo.AddComponent<InventoryDisplayUI>(inventoryPanel.gameObject);

            if (grid != null)
            {
                if (grid.GetComponent<GridLayoutGroup>() == null)
                    Undo.AddComponent<GridLayoutGroup>(grid.gameObject);

                var gridUi = grid.GetComponent<InventoryGridUI>();
                if (gridUi == null)
                    gridUi = Undo.AddComponent<InventoryGridUI>(grid.gameObject);

                var cellPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CellPrefabPath);
                var cellUi = cellPrefab != null ? cellPrefab.GetComponent<InventoryCellUI>() : null;
                if (cellUi == null && cellPrefab != null)
                {
                    // Cell prefab may need the component added on the asset once.
                    cellUi = cellPrefab.GetComponent<InventoryCellUI>() ?? cellPrefab.AddComponent<InventoryCellUI>();
                    EditorUtility.SetDirty(cellPrefab);
                }

                var gridSo = new SerializedObject(gridUi);
                if (cellUi != null)
                    gridSo.FindProperty("cellPrefab").objectReferenceValue = cellUi;
                gridSo.FindProperty("columns").intValue = 7;
                gridSo.FindProperty("rows").intValue = 6;
                gridSo.FindProperty("fillParent").boolValue = true;
                gridSo.ApplyModifiedPropertiesWithoutUndo();
            }

            var itemLayer = inventoryPanel.Find("ItemLayer");
            if (itemLayer == null)
            {
                var layerGo = new GameObject("ItemLayer", typeof(RectTransform));
                Undo.RegisterCreatedObjectUndo(layerGo, "Create ItemLayer");
                itemLayer = layerGo.transform;
                itemLayer.SetParent(inventoryPanel, false);
                var rt = layerGo.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }

            // Keep canvas sort above world UI but below shop (20).
            var canvas = instance.GetComponent<Canvas>();
            if (canvas != null)
                canvas.sortingOrder = 15;

            return display;
        }

        private static void WireHandmadeDisplay(InventoryDisplayUI display, InventoryController inventory)
        {
            if (display == null) return;

            var so = new SerializedObject(display);
            var grid = Object.FindFirstObjectByType<InventoryGridUI>();

            if (inventory != null)
                so.FindProperty("inventory").objectReferenceValue = inventory;
            if (grid != null)
                so.FindProperty("gridUI").objectReferenceValue = grid;
            if (so.FindProperty("panelRoot").objectReferenceValue == null)
                so.FindProperty("panelRoot").objectReferenceValue = display.transform;

            var layer = display.transform.Find("ItemLayer");
            if (layer != null)
                so.FindProperty("itemLayer").objectReferenceValue = layer.GetComponent<RectTransform>();

            var itemPrefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ItemPrefabPath);
            var itemUiOnAsset = itemPrefabAsset != null ? itemPrefabAsset.GetComponent<InventoryItemUI>() : null;
            if (itemUiOnAsset == null && itemPrefabAsset != null)
            {
                itemUiOnAsset = itemPrefabAsset.AddComponent<InventoryItemUI>();
                EditorUtility.SetDirty(itemPrefabAsset);
            }

            if (itemUiOnAsset != null)
                so.FindProperty("itemPrefab").objectReferenceValue = itemUiOnAsset;

            so.ApplyModifiedPropertiesWithoutUndo();

            // Handmade uses legacy single-grid sizes (not attaché BackpackLayout).
            if (inventory != null && grid != null)
            {
                var invSo = new SerializedObject(inventory);
                invSo.FindProperty("backpackLayout").objectReferenceValue = null;
                invSo.FindProperty("enableCompartmentUpgrades").boolValue = false;
                invSo.FindProperty("gridWidth").intValue = grid.Columns;
                invSo.FindProperty("gridHeight").intValue = grid.Rows;
                invSo.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        public static void WireShopToHandmade()
        {
            var shop = Object.FindFirstObjectByType<ShopUIController>();
            var display = Object.FindFirstObjectByType<InventoryDisplayUI>();
            if (shop == null || display == null) return;

            var so = new SerializedObject(shop);
            var handmadeProp = so.FindProperty("handmadePlayerInventoryUi");
            if (handmadeProp != null)
                handmadeProp.objectReferenceValue = display;
            so.ApplyModifiedPropertiesWithoutUndo();

            if (shop.GetComponent<GameplayUiLayout>() == null)
                shop.gameObject.AddComponent<GameplayUiLayout>();
        }

        public static void EnsurePlaytestHarnessRefs()
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

            var module = es.GetComponent<InputSystemUIInputModule>();
            var bootstrap = es.GetComponent<BayouUiInputBootstrap>();
            if (bootstrap == null)
                bootstrap = Undo.AddComponent<BayouUiInputBootstrap>(es.gameObject);

            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
            if (actions != null && module != null)
            {
                var so = new SerializedObject(module);
                so.FindProperty("m_ActionsAsset").objectReferenceValue = actions;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            BayouUiInputBootstrap.Wire(module, actions);
        }

        private static InventoryController EnsureInventoryController(GameObject player)
        {
            var existing = Object.FindFirstObjectByType<InventoryController>();
            if (existing != null)
                return existing;

            if (player == null)
                player = new GameObject("Player");

            return player.GetComponent<InventoryController>() ?? Undo.AddComponent<InventoryController>(player);
        }

        private static void EnsurePlayerWallet(GameObject player, InventoryController inventory)
        {
            if (Object.FindFirstObjectByType<PlayerWallet>() != null)
                return;

            var host = player != null ? player : (inventory != null ? inventory.gameObject : new GameObject("PlayerWallet"));
            if (host.GetComponent<PlayerWallet>() == null)
                Undo.AddComponent<PlayerWallet>(host);
        }

        private static GameObject ResolvePlayer()
        {
            var tagged = GameObject.FindGameObjectWithTag("Player");
            if (tagged != null) return tagged;

            var byName = GameObject.Find("Player");
            if (byName != null) return byName;

            var motor = Object.FindFirstObjectByType<BayouCharacterMotor>();
            return motor != null ? motor.gameObject : null;
        }

        private static void TagAsPlayer(GameObject player)
        {
            if (player == null) return;
            if (!player.CompareTag("Player"))
            {
                player.tag = "Player";
                EditorUtility.SetDirty(player);
            }
        }

        private static Transform FindChildByName(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            foreach (Transform child in root)
            {
                var hit = FindChildByName(child, name);
                if (hit != null) return hit;
            }

            return null;
        }
    }
}
#endif
