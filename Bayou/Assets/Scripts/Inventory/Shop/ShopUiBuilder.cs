using Bayou.Inventory.UI;
using Bayou.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Bayou.Inventory.Shop
{
    /// <summary>
    /// Builds a MockUI-styled shop canvas when the scene has none (InventoryTest / playtest).
    /// </summary>
    public static class ShopUiBuilder
    {
        public static ShopUIController EnsureInScene(ShopDefinition shopDef = null)
        {
            var existing = Object.FindFirstObjectByType<ShopUIController>();
            if (existing != null && existing.HasPanels)
            {
                WireHandmade(existing);
                if (shopDef != null)
                    existing.AssignShopDefinition(shopDef);
                return existing;
            }

            if (existing != null)
                Object.Destroy(existing.gameObject);

            return Build(shopDef);
        }

        public static ShopUIController Build(ShopDefinition shopDef = null)
        {
            var cellGo = LoadPrefab("Assets/Inventory/Prefabs/InventoryCell.prefab") ?? CreateRuntimeCellTemplate();
            var itemGo = LoadPrefab("Assets/Inventory/Prefabs/InventoryItemView.prefab") ?? CreateRuntimeItemTemplate();

            var canvasGo = new GameObject("ShopCanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            canvasGo.AddComponent<GameplayUiLayout>();

            var overlay = CreateRect("ShopOverlay", canvasGo.transform);
            var overlayRt = overlay.GetComponent<RectTransform>();
            StretchFull(overlayRt);
            var overlayImg = overlay.AddComponent<Image>();
            overlayImg.color = ShopUiStyle.OverlayDim;
            // Must not block the player bag (lower canvas) — sell drags start there.
            overlayImg.raycastTarget = false;

            var header = CreateBar("Header", overlay.transform, top: true, height: 56f);
            var merchantLabel = CreateTmp("MerchantName", header.transform, "Shopkeeper", 24, TextAlignmentOptions.MidlineLeft);
            var merchantRt = merchantLabel.rectTransform;
            merchantRt.anchorMin = new Vector2(0f, 0f);
            merchantRt.anchorMax = new Vector2(0.5f, 1f);
            merchantRt.offsetMin = new Vector2(28f, 0f);
            merchantRt.offsetMax = Vector2.zero;

            var balanceLabel = CreateTmp("Balance", header.transform, "$0", 24, TextAlignmentOptions.MidlineRight);
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

            var merchantGrid = CreateCasePanel("MerchantPanel", body.transform, "Shopkeeper", new Vector2(0.52f, 0f), new Vector2(1f, 1f));
            var playerGrid = CreateCasePanel("PlayerPanel", body.transform, "Personal", new Vector2(0f, 0f), new Vector2(0.48f, 1f));
            playerGrid.transform.parent.gameObject.SetActive(false);

            var merchantPanel = SetupBagPanel(merchantGrid, cellGo, itemGo);
            var playerPanel = SetupBagPanel(playerGrid, cellGo, itemGo);

            var footer = CreateBar("Footer", overlay.transform, top: false, height: 88f);
            var dealLabel = CreateTmp(
                "DealSummary",
                footer.transform,
                "Buying: $0  |  Selling: $0  |  Even trade",
                20,
                TextAlignmentOptions.Center);
            StretchFull(dealLabel.rectTransform);
            dealLabel.rectTransform.offsetMin = new Vector2(24f, 44f);
            dealLabel.rectTransform.offsetMax = new Vector2(-24f, -4f);

            var closeDealBtn = CreateButton("CloseDealButton", footer.transform, "Close Deal", new Vector2(-120f, 10f), ShopUiStyle.ButtonGreen);
            var cancelBtn = CreateButton("CancelButton", footer.transform, "Cancel", new Vector2(120f, 10f), ShopUiStyle.ButtonMuted);

            var shopUi = canvasGo.AddComponent<ShopUIController>();
            shopUi.WireBuiltUi(
                shopDef,
                overlayRt,
                playerPanel,
                merchantPanel,
                merchantLabel,
                balanceLabel,
                dealLabel,
                closeDealBtn,
                cancelBtn);

            WireHandmade(shopUi);
            overlay.SetActive(false);
            return shopUi;
        }

        public static void WireHandmade(ShopUIController shopUi)
        {
            if (shopUi == null) return;
            var display = InventoryDisplayUI.Active ?? Object.FindFirstObjectByType<InventoryDisplayUI>();
            if (display != null)
                shopUi.AssignHandmadeInventory(display);
        }

        private static GameObject LoadPrefab(string assetPath)
        {
#if UNITY_EDITOR
            var fromEditor = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (fromEditor != null)
                return fromEditor;
#endif
            // Player builds cannot use AssetDatabase — prefer Resources copies.
            if (assetPath.EndsWith("InventoryCell.prefab", System.StringComparison.OrdinalIgnoreCase))
                return Resources.Load<GameObject>("Bayou/UI/InventoryCell");
            if (assetPath.EndsWith("InventoryItemView.prefab", System.StringComparison.OrdinalIgnoreCase))
                return Resources.Load<GameObject>("Bayou/UI/InventoryItemView");
            return null;
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
            bg.raycastTarget = true;

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

        private static InventoryBagPanelUI SetupBagPanel(GameObject gridArea, GameObject cellPrefab, GameObject itemPrefab)
        {
            var panelUi = gridArea.AddComponent<InventoryBagPanelUI>();
            var bg = gridArea.transform.parent.GetComponent<Image>();
            panelUi.ApplyMockUiChrome(bg, cellPrefab, itemPrefab);
            return panelUi;
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
            tmp.color = ShopUiStyle.TextCream;
            tmp.raycastTarget = false;
            if (TMP_Settings.defaultFontAsset != null)
                tmp.font = TMP_Settings.defaultFontAsset;
            return tmp;
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

        private static GameObject CreateRuntimeCellTemplate()
        {
            // Keep active: Instantiate from an inactive template yields inactive clones (empty shop grid).
            var go = new GameObject("InventoryCell_Runtime", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            Object.DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            var img = go.GetComponent<Image>();
            img.color = ShopUiStyle.CellCream;
            img.raycastTarget = true;
            img.sprite = UiWhiteSprite.Get();
            var rt = go.GetComponent<RectTransform>();
            rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 0f);
            rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 0f);
            return go;
        }

        private static GameObject CreateRuntimeItemTemplate()
        {
            var go = new GameObject("InventoryItemView_Runtime", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            Object.DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            var bg = go.GetComponent<Image>();
            bg.color = new Color(0.55f, 0.42f, 0.28f, 0.95f);
            bg.raycastTarget = true;
            bg.sprite = UiWhiteSprite.Get();

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            iconGo.transform.SetParent(go.transform, false);
            var iconRt = iconGo.GetComponent<RectTransform>();
            StretchFull(iconRt);
            iconRt.offsetMin = new Vector2(4f, 4f);
            iconRt.offsetMax = new Vector2(-4f, -4f);
            var icon = iconGo.GetComponent<Image>();
            icon.raycastTarget = true;
            icon.preserveAspect = false;
            icon.sprite = UiWhiteSprite.Get();

            var view = go.AddComponent<InventoryItemView>();
            view.BindImages(icon, bg);
            return go;
        }
    }
}
