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
                AddStock(stock, fish, 0);
                shopSo.ApplyModifiedPropertiesWithoutUndo();
            }

            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null && player.GetComponent<PlayerWallet>() == null)
                player.AddComponent<PlayerWallet>();

            if (Object.FindFirstObjectByType<PlayerWallet>() == null)
            {
                var inv = Object.FindFirstObjectByType<InventoryController>();
                var host = inv != null ? inv.gameObject : new GameObject("PlayerWallet");
                if (host.GetComponent<PlayerWallet>() == null)
                    host.AddComponent<PlayerWallet>();
            }

            // Never call CreateInventoryUi() here — that spawns the dark procedural InventoryCanvas
            // and replaces the handmade brown MockUI bag. Shop only needs cell/item view prefabs.
            var cellPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Inventory/Prefabs/InventoryCell.prefab");
            var itemPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Inventory/Prefabs/InventoryItemView.prefab");
            if (cellPrefab == null || itemPrefab == null)
            {
                Debug.LogWarning(
                    "[Bayou] Missing InventoryCell/InventoryItemView prefabs under Assets/Inventory/Prefabs. " +
                    "Shop merchant panel needs those; your brown UIGrid bag is separate (MockUI).");
            }

            var existing = Object.FindFirstObjectByType<ShopUIController>();
            if (existing != null && !forceRecreate)
            {
                EnsureShopkeeper(existing, shopDef);
                WireHandmade(existing);
                Selection.activeObject = existing.gameObject;
                Debug.Log("[Bayou] Shop UI already exists — updated shopkeeper + handmade wiring.");
                return;
            }

            if (existing != null)
                Object.DestroyImmediate(existing.gameObject);

            var canvasGo = new GameObject("ShopCanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            canvasGo.AddComponent<GameplayUiLayout>();

            var overlay = CreateRect("ShopOverlay", canvasGo.transform);
            var overlayRt = overlay.GetComponent<RectTransform>();
            StretchFull(overlayRt);
            var overlayImg = overlay.AddComponent<Image>();
            overlayImg.color = ShopUiStyle.OverlayDim;
            overlayImg.raycastTarget = false;

            var header = CreateBar("Header", overlay.transform, top: true, 56f);
            var merchantLabel = CreateTmp("MerchantName", header.transform, "Shopkeeper", 24, TextAlignmentOptions.MidlineLeft);
            var merchantRt = merchantLabel.rectTransform;
            merchantRt.anchorMin = new Vector2(0f, 0f);
            merchantRt.anchorMax = new Vector2(0.5f, 1f);
            merchantRt.offsetMin = new Vector2(28f, 0f);
            merchantRt.offsetMax = Vector2.zero;

            var balanceLabel = CreateTmp("Balance", header.transform, "$500", 24, TextAlignmentOptions.MidlineRight);
            var balanceRt = balanceLabel.rectTransform;
            balanceRt.anchorMin = new Vector2(0.5f, 0f);
            balanceRt.anchorMax = new Vector2(1f, 1f);
            balanceRt.offsetMin = Vector2.zero;
            balanceRt.offsetMax = new Vector2(-28f, 0f);

            var body = CreateRect("Body", overlay.transform);
            var bodyRt = body.GetComponent<RectTransform>();
            bodyRt.anchorMin = new Vector2(0f, 0f);
            bodyRt.anchorMax = new Vector2(1f, 1f);
            bodyRt.offsetMin = new Vector2(16f, 88f);
            bodyRt.offsetMax = new Vector2(-16f, -56f);

            var merchantPanelGo = CreateCasePanel("MerchantPanel", body.transform, "Shopkeeper", new Vector2(0.52f, 0f), new Vector2(1f, 1f));
            var playerPanelGo = CreateCasePanel("PlayerPanel", body.transform, "Personal", new Vector2(0f, 0f), new Vector2(0.48f, 1f));
            playerPanelGo.transform.parent.gameObject.SetActive(false);

            var merchantPanel = SetupBagPanel(merchantPanelGo, cellPrefab, itemPrefab, layout);
            var playerPanel = SetupBagPanel(playerPanelGo, cellPrefab, itemPrefab, layout);

            var footer = CreateBar("Footer", overlay.transform, top: false, 88f);
            var dealLabel = CreateTmp("DealSummary", footer.transform, "Buying: $0  |  Selling: $0  |  Even trade", 20, TextAlignmentOptions.Center);
            StretchFull(dealLabel.rectTransform);
            dealLabel.rectTransform.offsetMin = new Vector2(24f, 44f);
            dealLabel.rectTransform.offsetMax = new Vector2(-24f, -4f);

            var closeDealBtn = CreateButton("CloseDealButton", footer.transform, "Close Deal", new Vector2(-120f, 10f), ShopUiStyle.ButtonGreen);
            var cancelBtn = CreateButton("CancelButton", footer.transform, "Cancel", new Vector2(120f, 10f), ShopUiStyle.ButtonMuted);

            var shopUi = canvasGo.AddComponent<ShopUIController>();
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

            WireHandmade(shopUi);
            overlay.SetActive(false);

            EnsureShopkeeper(shopUi, shopDef, new Vector3(3f, 0f, 3f));

            Selection.activeGameObject = canvasGo;
            Debug.Log("[Bayou] MockUI shop created. Shift+5 or walk to Shopkeeper (E). Drag between bags; Close Deal to pay.");
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

        private static void WireHandmade(ShopUIController shopUi)
        {
            var display = Object.FindFirstObjectByType<InventoryDisplayUI>();
            if (shopUi == null || display == null) return;
            var so = new SerializedObject(shopUi);
            so.FindProperty("handmadePlayerInventoryUi").objectReferenceValue = display;
            so.ApplyModifiedPropertiesWithoutUndo();
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

        private static GameObject CreateBar(string name, Transform parent, bool top, float height)
        {
            var bar = CreateRect(name, parent);
            var rt = bar.GetComponent<RectTransform>();
            if (top)
            {
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
            }
            else
            {
                rt.anchorMin = new Vector2(0f, 0f);
                rt.anchorMax = new Vector2(1f, 0f);
                rt.pivot = new Vector2(0.5f, 0f);
            }

            rt.sizeDelta = new Vector2(0f, height);
            rt.anchoredPosition = Vector2.zero;
            var bg = bar.AddComponent<Image>();
            bg.color = ShopUiStyle.HeaderFooter;
            bg.raycastTarget = false;
            return bar;
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
            tmp.color = ShopUiStyle.TextCream;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static GameObject CreateCasePanel(string name, Transform parent, string title, Vector2 anchorMin, Vector2 anchorMax)
        {
            var panel = CreateRect(name, parent);
            var rt = panel.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = new Vector2(8f, 8f);
            rt.offsetMax = new Vector2(-8f, -8f);

            var bg = panel.AddComponent<Image>();
            bg.color = ShopUiStyle.PanelBrown;

            var label = CreateTmp("Title", panel.transform, title, 22, TextAlignmentOptions.Top);
            var labelRt = label.rectTransform;
            labelRt.anchorMin = new Vector2(0f, 1f);
            labelRt.anchorMax = new Vector2(1f, 1f);
            labelRt.pivot = new Vector2(0.5f, 1f);
            labelRt.sizeDelta = new Vector2(0f, 32f);
            labelRt.anchoredPosition = new Vector2(0f, -8f);

            var gridArea = CreateRect("GridArea", panel.transform);
            var gridRt = gridArea.GetComponent<RectTransform>();
            gridRt.anchorMin = Vector2.zero;
            gridRt.anchorMax = Vector2.one;
            gridRt.offsetMin = new Vector2(20f, 20f);
            gridRt.offsetMax = new Vector2(-20f, -44f);
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
            so.FindProperty("clipItemsToGrid").boolValue = true;
            so.FindProperty("gridPanelPadding").floatValue = 12f;
            so.FindProperty("panelBackgroundColor").colorValue = ShopUiStyle.PanelBrown;
            so.FindProperty("cellEmptyColor").colorValue = ShopUiStyle.CellCream;
            so.FindProperty("cellHoverValidColor").colorValue = ShopUiStyle.HoverValid;
            so.FindProperty("cellHoverInvalidColor").colorValue = ShopUiStyle.HoverInvalid;
            so.ApplyModifiedPropertiesWithoutUndo();
            return panelUi;
        }

        private static Button CreateButton(string name, Transform parent, string label, Vector2 anchoredPosition, Color color)
        {
            var go = CreateRect(name, parent);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(180f, 40f);
            rt.anchoredPosition = anchoredPosition;

            var img = go.AddComponent<Image>();
            img.color = color;

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;

            var text = CreateTmp("Text", go.transform, label, 18, TextAlignmentOptions.Center);
            StretchFull(text.rectTransform);
            return btn;
        }
    }
}
#endif
