#if UNITY_EDITOR
using Bayou.Inventory.UI;
using Bayou.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Bayou.Inventory.Editor
{
    public static class InventorySetupMenu
    {
        [MenuItem("Bayou/Inventory/Setup Basic Inventory In Scene", false, 0)]
        public static void SetupBasicInventoryInScene()
        {
            CreateSampleItems();
            CreateCaseLayoutAsset();
            CreateInventoryUi();

            var layout = AssetDatabase.LoadAssetAtPath<BackpackLayoutDefinition>("Assets/Inventory/BackpackLayout_Case.asset");
            var fish = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Inventory/Items/Item_Fish.asset");
            var herb = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Inventory/Items/Item_Herb.asset");

            var controller = Object.FindFirstObjectByType<InventoryController>();
            if (controller != null)
            {
                var ctrlSo = new SerializedObject(controller);
                ctrlSo.FindProperty("backpackLayout").objectReferenceValue = layout;
                ctrlSo.FindProperty("enableCompartmentUpgrades").boolValue = false;
                var starters = ctrlSo.FindProperty("startingItems");
                starters.arraySize = 0;
                if (fish != null) { starters.arraySize++; starters.GetArrayElementAtIndex(0).objectReferenceValue = fish; }
                if (herb != null)
                {
                    starters.arraySize++;
                    starters.GetArrayElementAtIndex(starters.arraySize - 1).objectReferenceValue = herb;
                }
                ctrlSo.ApplyModifiedPropertiesWithoutUndo();
            }

            var ui = Object.FindFirstObjectByType<InventoryUIController>();
            if (ui != null)
            {
                var uiSo = new SerializedObject(ui);
                uiSo.FindProperty("gridFillsPanel").boolValue = true;
                uiSo.FindProperty("clipItemsToGrid").boolValue = true;
                uiSo.ApplyModifiedPropertiesWithoutUndo();
            }

            InventoryInputWiring.WireInventoryActionsInScene();

            Debug.Log("[Bayou] Basic inventory ready: 7×6 grid fills panel, items clip to grid. Assign Toggle/Rotate on InventoryUIController.");
        }

        [MenuItem("Bayou/Inventory/Create Default Backpack Layout Asset (3 pockets, shelved)")]
        public static void CreateBackpackLayoutAsset()
        {
            System.IO.Directory.CreateDirectory("Assets/Inventory");
            var path = "Assets/Inventory/BackpackLayout_Default.asset";
            if (AssetDatabase.LoadAssetAtPath<BackpackLayoutDefinition>(path) != null)
            {
                Debug.Log("[Bayou] BackpackLayout_Default already exists.");
                return;
            }

            var layout = ScriptableObject.CreateInstance<BackpackLayoutDefinition>();
            BackpackLayoutDefinition.ApplyMultiCompartmentBackpackPreset(layout);
            AssetDatabase.CreateAsset(layout, path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = layout;
            Debug.Log("[Bayou] Created BackpackLayout_Default (3-pocket, upgrade-gated). Enable compartment upgrades on InventoryController to use locks.");
        }

        [MenuItem("Bayou/Inventory/Create Attaché Case Layout Asset (7×6)")]
        public static void CreateCaseLayoutAsset()
        {
            System.IO.Directory.CreateDirectory("Assets/Inventory");
            var path = "Assets/Inventory/BackpackLayout_Case.asset";
            var existing = AssetDatabase.LoadAssetAtPath<BackpackLayoutDefinition>(path);
            if (existing != null)
            {
                Selection.activeObject = existing;
                Debug.Log("[Bayou] BackpackLayout_Case already exists.");
                return;
            }

            var layout = ScriptableObject.CreateInstance<BackpackLayoutDefinition>();
            BackpackLayoutDefinition.ApplyAttachéCasePreset(layout);
            AssetDatabase.CreateAsset(layout, path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = layout;
            Debug.Log("[Bayou] Created BackpackLayout_Case (7×6 grid, bottom-right). Assign on InventoryController.");
        }

        [MenuItem("Bayou/Inventory/Create Backpack UI In Scene (3 pockets, shelved)", false, 100)]
        public static void CreateBackpackInventoryUi()
        {
            var layoutPath = "Assets/Inventory/BackpackLayout_Default.asset";
            var layout = AssetDatabase.LoadAssetAtPath<BackpackLayoutDefinition>(layoutPath);
            if (layout == null)
                CreateBackpackLayoutAsset();
            layout = AssetDatabase.LoadAssetAtPath<BackpackLayoutDefinition>(layoutPath);

            if (Object.FindFirstObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<InputSystemUIInputModule>();
                es.AddComponent<BayouUiInputBootstrap>();
            }

            var canvasGo = new GameObject("InventoryCanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGo.AddComponent<GraphicRaycaster>();

            var panel = CreateRect("InventoryPanel", canvasGo.transform);
            var panelRt = panel.GetComponent<RectTransform>();
            panelRt.anchorMin = panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.pivot = new Vector2(0.5f, 0.5f);
            panelRt.sizeDelta = layout != null ? layout.panelSize : new Vector2(280, 520);
            panelRt.anchoredPosition = Vector2.zero;

            var panelImg = panel.AddComponent<Image>();
            panelImg.color = Color.white;
            if (layout != null && layout.backgroundSprite != null)
            {
                panelImg.sprite = layout.backgroundSprite;
                panelImg.preserveAspect = true;
            }
            else
            {
                panelImg.color = new Color(0.45f, 0.32f, 0.18f, 1f);
            }

            var cellPrefab = CreateCellPrefab();
            var itemPrefab = CreateItemPrefab();

            var controller = Object.FindFirstObjectByType<InventoryController>();
            if (controller == null)
            {
                var player = GameObject.FindGameObjectWithTag("Player") ?? new GameObject("Player");
                controller = player.GetComponent<InventoryController>();
                if (controller == null)
                    controller = player.AddComponent<InventoryController>();
            }

            var ctrlSo = new SerializedObject(controller);
            ctrlSo.FindProperty("backpackLayout").objectReferenceValue = layout;
            ctrlSo.FindProperty("enableCompartmentUpgrades").boolValue = false;
            ctrlSo.ApplyModifiedPropertiesWithoutUndo();

            var ui = canvasGo.AddComponent<InventoryUIController>();
            var uiSo = new SerializedObject(ui);
            uiSo.FindProperty("inventory").objectReferenceValue = controller;
            uiSo.FindProperty("panelRoot").objectReferenceValue = panelRt;
            uiSo.FindProperty("backpackBackgroundImage").objectReferenceValue = panelImg;
            uiSo.FindProperty("cellPrefab").objectReferenceValue = cellPrefab;
            uiSo.FindProperty("itemViewPrefab").objectReferenceValue = itemPrefab;
            uiSo.ApplyModifiedPropertiesWithoutUndo();

            panel.SetActive(false);
            Selection.activeGameObject = canvasGo;
            Debug.Log("[Bayou] Backpack inventory UI created. Assign your backpack PNG to BackpackLayout_Default → Background Sprite, then nudge compartment positions.");
        }

        [MenuItem("Bayou/Inventory/Create Attaché Case UI In Scene (7×6 bottom-right)")]
        public static void CreateInventoryUi()
        {
            var layoutPath = "Assets/Inventory/BackpackLayout_Case.asset";
            var layout = AssetDatabase.LoadAssetAtPath<BackpackLayoutDefinition>(layoutPath);
            if (layout == null)
                CreateCaseLayoutAsset();
            layout = AssetDatabase.LoadAssetAtPath<BackpackLayoutDefinition>(layoutPath);

            if (Object.FindFirstObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<InputSystemUIInputModule>();
                es.AddComponent<BayouUiInputBootstrap>();
            }

            var canvasGo = new GameObject("InventoryCanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGo.AddComponent<GraphicRaycaster>();

            var panel = CreateRect("InventoryPanel", canvasGo.transform);
            var panelRt = panel.GetComponent<RectTransform>();
            panelRt.anchorMin = panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.pivot = new Vector2(0.5f, 0.5f);
            panelRt.sizeDelta = layout != null ? layout.panelSize : new Vector2(640, 480);
            panelRt.anchoredPosition = Vector2.zero;
            var panelImg = panel.AddComponent<Image>();
            panelImg.color = new Color(0.08f, 0.09f, 0.11f, 0.94f);

            var cellPrefab = CreateCellPrefab();
            var itemPrefab = CreateItemPrefab();

            var controller = Object.FindFirstObjectByType<InventoryController>();
            if (controller == null)
            {
                var player = GameObject.FindGameObjectWithTag("Player") ?? new GameObject("Player");
                controller = player.GetComponent<InventoryController>();
                if (controller == null)
                    controller = player.AddComponent<InventoryController>();
            }

            var ctrlSo = new SerializedObject(controller);
            ctrlSo.FindProperty("backpackLayout").objectReferenceValue = layout;
            ctrlSo.FindProperty("enableCompartmentUpgrades").boolValue = false;
            ctrlSo.ApplyModifiedPropertiesWithoutUndo();

            var ui = canvasGo.AddComponent<InventoryUIController>();
            var so = new SerializedObject(ui);
            so.FindProperty("inventory").objectReferenceValue = controller;
            so.FindProperty("panelRoot").objectReferenceValue = panelRt;
            so.FindProperty("backpackBackgroundImage").objectReferenceValue = panelImg;
            so.FindProperty("cellPrefab").objectReferenceValue = cellPrefab;
            so.FindProperty("itemViewPrefab").objectReferenceValue = itemPrefab;
            so.FindProperty("gridFillsPanel").boolValue = true;
            so.FindProperty("clipItemsToGrid").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();

            InventoryInputWiring.WireInventoryActionsInScene();

            panel.SetActive(false);
            Selection.activeGameObject = canvasGo;
            Debug.Log("[Bayou] Attaché case UI created. Grid fills the panel; items clip and snap to cells. Assign Toggle/Rotate InputActionReferences.");
        }

        private const string ShelvedUpgradeMenu = "Bayou/Inventory/(Shelved) Create Sample Backpack Upgrades";

        [MenuItem(ShelvedUpgradeMenu, true)]
        private static bool ShelvedUpgradeMenuDisabled() => false;

        [MenuItem(ShelvedUpgradeMenu, false, 200)]
        public static void CreateSampleUpgrades()
        {
            System.IO.Directory.CreateDirectory("Assets/Inventory/Upgrades");
            CreateUpgrade("Upgrade_MiddlePocket", "Middle pocket expansion", "upgrade_middle", "middle");
            CreateUpgrade("Upgrade_TopPocket", "Top pocket expansion", "upgrade_top", "top");
            AssetDatabase.SaveAssets();
            Debug.Log("[Bayou] Sample upgrades created. Enable compartment upgrades on InventoryController before using.");
        }

        private static void CreateUpgrade(string file, string title, string upgradeId, string compartmentId)
        {
            var path = $"Assets/Inventory/Upgrades/{file}.asset";
            if (AssetDatabase.LoadAssetAtPath<BackpackUpgradeDefinition>(path) != null) return;
            var u = ScriptableObject.CreateInstance<BackpackUpgradeDefinition>();
            u.displayName = title;
            u.upgradeId = upgradeId;
            u.unlocksCompartmentId = compartmentId;
            AssetDatabase.CreateAsset(u, path);
        }

        [MenuItem("Bayou/Inventory/Create Sample Item Assets")]
        public static void CreateSampleItems()
        {
            CreateItem("Item_Fish", "Caught Fish", ItemShape.Rectangle(2, 1), isFish: true);
            CreateItem("Item_NetPatch", "Net Patch", ItemShape.LShape());
            CreateItem("Item_Herb", "Bayou Herb", ItemShape.Rectangle(1, 2));
            AssetDatabase.SaveAssets();
            Debug.Log("[Bayou] Sample items created under Assets/Inventory/Items/");
        }

        private static void CreateItem(string file, string name, ItemShape shape, bool isFish = false)
        {
            var path = $"Assets/Inventory/Items/{file}.asset";
            System.IO.Directory.CreateDirectory("Assets/Inventory/Items");
            var existing = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
            if (existing != null) return;
            var item = ScriptableObject.CreateInstance<ItemDefinition>();
            item.displayName = name;
            item.shape = shape;
            item.isFish = isFish;
            AssetDatabase.CreateAsset(item, path);
        }

        private static GameObject CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static GameObject CreateCellPrefab()
        {
            var path = "Assets/Inventory/Prefabs/InventoryCell.prefab";
            System.IO.Directory.CreateDirectory("Assets/Inventory/Prefabs");
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            var go = CreateRect("InventoryCell", null);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.15f, 0.17f, 0.2f, 1f);
            go.AddComponent<InventoryCellDropTarget>();
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static GameObject CreateItemPrefab()
        {
            var path = "Assets/Inventory/Prefabs/InventoryItemView.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            var go = CreateRect("InventoryItemView", null);
            var rt = go.GetComponent<RectTransform>();
            rt.pivot = new Vector2(0, 1);
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);

            var bg = go.AddComponent<Image>();
            bg.color = new Color(0.25f, 0.35f, 0.45f, 0.85f);

            var iconGo = CreateRect("Icon", go.transform);
            var iconRt = iconGo.GetComponent<RectTransform>();
            iconRt.anchorMin = Vector2.zero;
            iconRt.anchorMax = Vector2.one;
            iconRt.offsetMin = new Vector2(4, 4);
            iconRt.offsetMax = new Vector2(-4, -4);
            var icon = iconGo.AddComponent<Image>();
            icon.preserveAspect = true;

            var view = go.AddComponent<InventoryItemView>();
            var so = new SerializedObject(view);
            so.FindProperty("iconImage").objectReferenceValue = icon;
            so.FindProperty("backgroundImage").objectReferenceValue = bg;
            so.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }
    }
}
#endif
