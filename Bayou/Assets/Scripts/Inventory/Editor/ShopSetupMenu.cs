#if UNITY_EDITOR
using Bayou.Inventory;
using Bayou.Inventory.Shop;
using Bayou.Inventory.UI;
using Bayou.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Bayou.Inventory.Editor
{
    public static class ShopSetupMenu
    {
        [MenuItem("Bayou/Shop/Setup Shop UI In Scene", false, 50)]
        public static void SetupShopInScene()
        {
            SetupShopInScene(forceRecreate: false);
        }

        public static void SetupShopInScene(bool forceRecreate)
        {
            InventorySetupMenu.CreateSampleItems();
            InventorySetupMenu.CreateCaseLayoutAsset();
            CreateSampleShopDefinition();

            var layout = AssetDatabase.LoadAssetAtPath<BackpackLayoutDefinition>("Assets/Inventory/BackpackLayout_Case.asset");
            var fish = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Inventory/Items/Item_Fish.asset");
            var herb = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Inventory/Items/Item_Herb.asset");
            var netPatch = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Inventory/Items/Item_NetPatch.asset");
            var shopDef = AssetDatabase.LoadAssetAtPath<ShopDefinition>("Assets/Inventory/Shop/Shop_BayouMerchant.asset");

            SetItemPrices(fish, buyPrice: 80, sellPrice: 40);
            SetItemPrices(herb, buyPrice: 25, sellPrice: 12);
            SetItemPrices(netPatch, buyPrice: 120, sellPrice: 60);

            if (shopDef != null)
            {
                var shopSo = new SerializedObject(shopDef);
                shopSo.FindProperty("layout").objectReferenceValue = layout;
                var stock = shopSo.FindProperty("stock");
                stock.arraySize = 0;
                AddStock(stock, herb, 0);
                AddStock(stock, netPatch, 0);
                shopSo.ApplyModifiedPropertiesWithoutUndo();
            }

            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null && player.GetComponent<PlayerWallet>() == null)
                player.AddComponent<PlayerWallet>();

            var cellPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Inventory/Prefabs/InventoryCell.prefab");
            var itemPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Inventory/Prefabs/InventoryItemView.prefab");
            if (cellPrefab == null || itemPrefab == null)
                InventorySetupMenu.CreateInventoryUi();

            cellPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Inventory/Prefabs/InventoryCell.prefab");
            itemPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Inventory/Prefabs/InventoryItemView.prefab");

            var existing = Object.FindFirstObjectByType<ShopUIController>();
            if (existing != null && !forceRecreate)
            {
                EnsureShopkeeper(existing, shopDef);
                Selection.activeObject = existing.gameObject;
                Debug.Log("[Bayou] Shop UI already exists — updated shopkeeper wiring.");
                return;
            }

            var canvasGo = new GameObject("ShopCanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            var overlay = CreateRect("ShopOverlay", canvasGo.transform);
            var overlayRt = overlay.GetComponent<RectTransform>();
            StretchFull(overlayRt);
            var overlayImg = overlay.AddComponent<Image>();
            overlayImg.color = new Color(0f, 0f, 0f, 0.45f);

            var header = CreateRect("Header", overlay.transform);
            var headerRt = header.GetComponent<RectTransform>();
            headerRt.anchorMin = new Vector2(0f, 1f);
            headerRt.anchorMax = new Vector2(1f, 1f);
            headerRt.pivot = new Vector2(0.5f, 1f);
            headerRt.sizeDelta = new Vector2(0f, 48f);
            headerRt.anchoredPosition = Vector2.zero;

            var merchantLabel = CreateTmp("MerchantName", header.transform, "Shopkeeper", 22, TextAlignmentOptions.MidlineLeft);
            var merchantRt = merchantLabel.rectTransform;
            merchantRt.anchorMin = new Vector2(0f, 0f);
            merchantRt.anchorMax = new Vector2(0.5f, 1f);
            merchantRt.offsetMin = new Vector2(24f, 0f);
            merchantRt.offsetMax = Vector2.zero;

            var balanceLabel = CreateTmp("Balance", header.transform, "$500", 22, TextAlignmentOptions.MidlineRight);
            var balanceRt = balanceLabel.rectTransform;
            balanceRt.anchorMin = new Vector2(0.5f, 0f);
            balanceRt.anchorMax = new Vector2(1f, 1f);
            balanceRt.offsetMin = Vector2.zero;
            balanceRt.offsetMax = new Vector2(-24f, 0f);

            var body = CreateRect("Body", overlay.transform);
            var bodyRt = body.GetComponent<RectTransform>();
            bodyRt.anchorMin = new Vector2(0f, 0f);
            bodyRt.anchorMax = new Vector2(1f, 1f);
            bodyRt.offsetMin = new Vector2(16f, 72f);
            bodyRt.offsetMax = new Vector2(-16f, -48f);

            var merchantPanelGo = CreatePanel("MerchantPanel", body.transform, "Shopkeeper", new Vector2(0.52f, 0f), new Vector2(1f, 1f));
            var playerPanelGo = CreatePanel("PlayerPanel", body.transform, "Personal", new Vector2(0f, 0f), new Vector2(0.48f, 1f));
            playerPanelGo.SetActive(false);

            var merchantPanel = SetupBagPanel(merchantPanelGo, cellPrefab, itemPrefab, layout);
            var playerPanel = SetupBagPanel(playerPanelGo, cellPrefab, itemPrefab, layout);

            var footer = CreateRect("Footer", overlay.transform);
            var footerRt = footer.GetComponent<RectTransform>();
            footerRt.anchorMin = new Vector2(0f, 0f);
            footerRt.anchorMax = new Vector2(1f, 0f);
            footerRt.pivot = new Vector2(0.5f, 0f);
            footerRt.sizeDelta = new Vector2(0f, 80f);
            footerRt.anchoredPosition = Vector2.zero;

            var dealLabel = CreateTmp("DealSummary", footer.transform, "Buying: $0  |  Selling: $0  |  Even trade", 18, TextAlignmentOptions.Center);
            StretchFull(dealLabel.rectTransform);
            dealLabel.rectTransform.offsetMin = new Vector2(24f, 40f);
            dealLabel.rectTransform.offsetMax = new Vector2(-24f, 0f);

            var closeDealBtn = CreateButton("CloseDealButton", footer.transform, "Close Deal", new Vector2(-120f, 8f));
            var cancelBtn = CreateButton("CancelButton", footer.transform, "Cancel", new Vector2(120f, 8f));

            var shopUi = canvasGo.AddComponent<ShopUIController>();
            canvasGo.AddComponent<GameplayUiLayout>();
            var shopUiSo = new SerializedObject(shopUi);
            shopUiSo.FindProperty("shopDefinition").objectReferenceValue = shopDef;
            shopUiSo.FindProperty("overlayRoot").objectReferenceValue = overlayRt;
            shopUiSo.FindProperty("playerPanel").objectReferenceValue = playerPanel;
            shopUiSo.FindProperty("merchantPanel").objectReferenceValue = merchantPanel;
            shopUiSo.FindProperty("merchantNameLabel").objectReferenceValue = merchantLabel;
            shopUiSo.FindProperty("balanceLabel").objectReferenceValue = balanceLabel;
            shopUiSo.FindProperty("dealSummaryLabel").objectReferenceValue = dealLabel;
            shopUiSo.FindProperty("closeDealButton").objectReferenceValue = closeDealBtn;
            shopUiSo.FindProperty("cancelButton").objectReferenceValue = cancelBtn;
            shopUiSo.ApplyModifiedPropertiesWithoutUndo();

            overlay.SetActive(false);

            EnsureShopkeeper(shopUi, shopDef, new Vector3(3f, 0f, 3f));

            Selection.activeGameObject = canvasGo;
            Debug.Log("[Bayou] Shop UI created. Walk to Shopkeeper and press E. Drag items between panels; Close Deal to pay.");
        }

        public static void EnsureShopkeeper(ShopUIController shopUi, ShopDefinition shopDef, Vector3? position = null)
        {
            if (shopUi == null || shopDef == null) return;

            var keeper = Object.FindFirstObjectByType<Shopkeeper>();
            if (keeper == null)
            {
                var keeperGo = new GameObject("Shopkeeper");
                keeper = keeperGo.AddComponent<Shopkeeper>();
            }

            if (position.HasValue)
                keeper.transform.position = position.Value;

            var keeperSo = new SerializedObject(keeper);
            keeperSo.FindProperty("shop").objectReferenceValue = shopDef;
            keeperSo.FindProperty("shopUi").objectReferenceValue = shopUi;
            keeperSo.ApplyModifiedPropertiesWithoutUndo();
        }

        [MenuItem("Bayou/Shop/Create Sample Shop Definition")]
        public static void CreateSampleShopDefinition()
        {
            System.IO.Directory.CreateDirectory("Assets/Inventory/Shop");
            var path = "Assets/Inventory/Shop/Shop_BayouMerchant.asset";
            if (AssetDatabase.LoadAssetAtPath<ShopDefinition>(path) != null)
                return;

            var shop = ScriptableObject.CreateInstance<ShopDefinition>();
            shop.merchantName = "Bayou Trader";
            AssetDatabase.CreateAsset(shop, path);
            AssetDatabase.SaveAssets();
        }

        private static void SetItemPrices(ItemDefinition item, int buyPrice, int sellPrice)
        {
            if (item == null) return;
            item.buyPrice = buyPrice;
            item.sellPrice = sellPrice;
            EditorUtility.SetDirty(item);
        }

        private static void AddStock(SerializedProperty stock, ItemDefinition item, int rotation)
        {
            if (item == null) return;
            stock.arraySize++;
            var entry = stock.GetArrayElementAtIndex(stock.arraySize - 1);
            entry.FindPropertyRelative("item").objectReferenceValue = item;
            entry.FindPropertyRelative("rotation").intValue = rotation;
        }

        private static GameObject CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static TextMeshProUGUI CreateTmp(string name, Transform parent, string text, float size, TextAlignmentOptions align)
        {
            var go = CreateRect(name, parent);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.alignment = align;
            tmp.color = Color.white;
            return tmp;
        }

        private static GameObject CreatePanel(string name, Transform parent, string title, Vector2 anchorMin, Vector2 anchorMax)
        {
            var panel = CreateRect(name, parent);
            var rt = panel.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var bg = panel.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.09f, 0.11f, 0.94f);

            var label = CreateTmp("Title", panel.transform, title, 20, TextAlignmentOptions.Top);
            var labelRt = label.rectTransform;
            labelRt.anchorMin = new Vector2(0f, 1f);
            labelRt.anchorMax = new Vector2(1f, 1f);
            labelRt.pivot = new Vector2(0.5f, 1f);
            labelRt.sizeDelta = new Vector2(0f, 28f);
            labelRt.anchoredPosition = new Vector2(0f, -4f);

            var gridArea = CreateRect("GridArea", panel.transform);
            var gridRt = gridArea.GetComponent<RectTransform>();
            gridRt.anchorMin = Vector2.zero;
            gridRt.anchorMax = Vector2.one;
            gridRt.offsetMin = new Vector2(8f, 8f);
            gridRt.offsetMax = new Vector2(-8f, -36f);

            return gridArea;
        }

        private static InventoryBagPanelUI SetupBagPanel(
            GameObject gridArea,
            GameObject cellPrefab,
            GameObject itemPrefab,
            BackpackLayoutDefinition layout)
        {
            var panelUi = gridArea.AddComponent<InventoryBagPanelUI>();
            var bg = gridArea.transform.parent.GetComponent<Image>();
            var so = new SerializedObject(panelUi);
            so.FindProperty("panelRoot").objectReferenceValue = gridArea.GetComponent<RectTransform>();
            so.FindProperty("backgroundImage").objectReferenceValue = bg;
            so.FindProperty("cellPrefab").objectReferenceValue = cellPrefab;
            so.FindProperty("itemViewPrefab").objectReferenceValue = itemPrefab;
            so.FindProperty("layout").objectReferenceValue = layout;
            so.FindProperty("gridFillsPanel").boolValue = true;
            so.FindProperty("clipItemsToGrid").boolValue = true;
            so.FindProperty("gridPanelPadding").floatValue = 6f;
            so.ApplyModifiedPropertiesWithoutUndo();
            return panelUi;
        }

        private static Button CreateButton(string name, Transform parent, string label, Vector2 anchoredPosition)
        {
            var go = CreateRect(name, parent);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(160f, 36f);
            rt.anchoredPosition = anchoredPosition;

            var img = go.AddComponent<Image>();
            img.color = new Color(0.22f, 0.38f, 0.28f, 1f);

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;

            var text = CreateTmp("Text", go.transform, label, 18, TextAlignmentOptions.Center);
            StretchFull(text.rectTransform);

            return btn;
        }
    }
}
#endif
