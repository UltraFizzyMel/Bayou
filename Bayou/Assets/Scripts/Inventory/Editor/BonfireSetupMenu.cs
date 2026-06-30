#if UNITY_EDITOR
using Bayou.Inventory;
using Bayou.Save;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using TMPro;

namespace Bayou.Inventory.Editor
{
    public static class BonfireSetupMenu
    {
        [MenuItem("Bayou/Save/Setup Bonfire Save System In Scene", false, 60)]
        public static void SetupBonfireInScene()
        {
            MarkFishItems();
            var catalog = CreateOrRefreshItemCatalog();

            EnsureGameSaveSystem(catalog);
            CreateBonfireUiIfNeeded();
            CreateBonfireInWorld();

            Debug.Log("[Bayou] Bonfire save system ready. Catch a fish, rest at the bonfire, cook it to save.");
        }

        [MenuItem("Bayou/Save/Mark InventoryData Fish Assets")]
        public static void MarkFishItems()
        {
            var guids = AssetDatabase.FindAssets("t:ItemDefinition", new[] { "Assets/InventoryData", "Assets/Inventory/Items" });
            var count = 0;
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
                if (item == null) continue;

                var looksLikeFish = item.name.Contains("Fish", System.StringComparison.OrdinalIgnoreCase) ||
                                    item.displayName.Contains("Fish", System.StringComparison.OrdinalIgnoreCase);
                if (!looksLikeFish && !item.isFish) continue;

                item.isFish = true;
                if (item.displayName == "Item" || string.IsNullOrWhiteSpace(item.displayName))
                    item.displayName = "Caught Fish";
                if (item.shape.width <= 1 && item.shape.height <= 1)
                    item.shape = ItemShape.Rectangle(2, 1);
                item.sellPrice = item.sellPrice > 0 ? item.sellPrice : 40;
                item.buyPrice = item.buyPrice > 0 ? item.buyPrice : 80;
                EditorUtility.SetDirty(item);
                count++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[Bayou] Marked {count} fish item(s) as cookable.");
        }

        [MenuItem("Bayou/Save/Create Item Catalog Asset")]
        public static ItemCatalog CreateOrRefreshItemCatalog()
        {
            System.IO.Directory.CreateDirectory("Assets/Inventory");
            var path = "Assets/Inventory/ItemCatalog.asset";
            var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(path);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<ItemCatalog>();
                AssetDatabase.CreateAsset(catalog, path);
            }

            var guids = AssetDatabase.FindAssets("t:ItemDefinition");
            var items = new System.Collections.Generic.List<ItemDefinition>();
            foreach (var guid in guids)
            {
                var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (item != null)
                    items.Add(item);
            }

            catalog.SetItems(items.ToArray());
            EditorUtility.SetDirty(catalog);
            catalog.BuildLookup();
            AssetDatabase.SaveAssets();
            return catalog;
        }

        private static void EnsureGameSaveSystem(ItemCatalog catalog)
        {
            var existing = Object.FindFirstObjectByType<GameSaveSystem>();
            if (existing != null)
            {
                var so = new SerializedObject(existing);
                so.FindProperty("itemCatalog").objectReferenceValue = catalog;
                so.ApplyModifiedPropertiesWithoutUndo();
                return;
            }

            var go = new GameObject("GameSaveSystem");
            var save = go.AddComponent<GameSaveSystem>();
            var saveSo = new SerializedObject(save);
            saveSo.FindProperty("itemCatalog").objectReferenceValue = catalog;
            saveSo.FindProperty("loadSaveOnStart").boolValue = true;
            saveSo.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateBonfireUiIfNeeded()
        {
            if (Object.FindFirstObjectByType<BonfireUIController>() != null)
                return;

            if (Object.FindFirstObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<InputSystemUIInputModule>();
            }

            var canvasGo = new GameObject("BonfireCanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 25;
            canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGo.AddComponent<GraphicRaycaster>();

            var overlay = CreateRect("BonfireOverlay", canvasGo.transform);
            var overlayRt = overlay.GetComponent<RectTransform>();
            StretchFull(overlayRt);
            var overlayImg = overlay.AddComponent<Image>();
            overlayImg.color = new Color(0.02f, 0.02f, 0.04f, 0.82f);

            var panel = CreateRect("Panel", overlay.transform);
            var panelRt = panel.GetComponent<RectTransform>();
            panelRt.anchorMin = panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.sizeDelta = new Vector2(420f, 360f);
            var panelImg = panel.AddComponent<Image>();
            panelImg.color = new Color(0.1f, 0.08f, 0.06f, 0.96f);

            var title = CreateTmp("Title", panel.transform, "Bonfire", 26, TextAlignmentOptions.Top);
            StretchTop(title.rectTransform, 40f);

            var hint = CreateTmp("Hint", panel.transform,
                "Select a fish from your pack to cook. Resting at the fire saves your journey.",
                16, TextAlignmentOptions.Top);
            var hintRt = hint.rectTransform;
            hintRt.anchorMin = new Vector2(0f, 1f);
            hintRt.anchorMax = new Vector2(1f, 1f);
            hintRt.pivot = new Vector2(0.5f, 1f);
            hintRt.sizeDelta = new Vector2(-32f, 64f);
            hintRt.anchoredPosition = new Vector2(0f, -44f);

            var listRoot = CreateRect("FishList", panel.transform);
            var listRt = listRoot.GetComponent<RectTransform>();
            listRt.anchorMin = new Vector2(0f, 0f);
            listRt.anchorMax = new Vector2(1f, 1f);
            listRt.offsetMin = new Vector2(16f, 88f);
            listRt.offsetMax = new Vector2(-16f, -112f);
            var layout = listRoot.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 6f;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;

            var fishTemplate = CreateButton("FishEntryTemplate", listRoot.transform, "Caught Fish");
            fishTemplate.gameObject.SetActive(false);

            var status = CreateTmp("Status", panel.transform, string.Empty, 15, TextAlignmentOptions.Bottom);
            StretchBottom(status.rectTransform, 84f);

            var cookBtn = CreateButton("CookAndRestButton", panel.transform, "Cook & Rest");
            var cookRt = cookBtn.GetComponent<RectTransform>();
            cookRt.anchorMin = cookRt.anchorMax = new Vector2(0.5f, 0f);
            cookRt.pivot = new Vector2(0.5f, 0f);
            cookRt.sizeDelta = new Vector2(180f, 36f);
            cookRt.anchoredPosition = new Vector2(-96f, 16f);

            var cancelBtn = CreateButton("CancelButton", panel.transform, "Leave");
            var cancelRt = cancelBtn.GetComponent<RectTransform>();
            cancelRt.anchorMin = cancelRt.anchorMax = new Vector2(0.5f, 0f);
            cancelRt.pivot = new Vector2(0.5f, 0f);
            cancelRt.sizeDelta = new Vector2(120f, 36f);
            cancelRt.anchoredPosition = new Vector2(96f, 16f);

            var ui = canvasGo.AddComponent<BonfireUIController>();
            var uiSo = new SerializedObject(ui);
            uiSo.FindProperty("overlayRoot").objectReferenceValue = overlayRt;
            uiSo.FindProperty("titleLabel").objectReferenceValue = title;
            uiSo.FindProperty("hintLabel").objectReferenceValue = hint;
            uiSo.FindProperty("statusLabel").objectReferenceValue = status;
            uiSo.FindProperty("fishListRoot").objectReferenceValue = listRt;
            uiSo.FindProperty("fishEntryTemplate").objectReferenceValue = fishTemplate;
            uiSo.FindProperty("cookAndRestButton").objectReferenceValue = cookBtn;
            uiSo.FindProperty("cancelButton").objectReferenceValue = cancelBtn;
            uiSo.ApplyModifiedPropertiesWithoutUndo();

            overlay.SetActive(false);
        }

        private static void CreateBonfireInWorld()
        {
            if (Object.FindFirstObjectByType<BonfireInteractable>() != null)
                return;

            var bonfire = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            bonfire.name = "Bonfire";
            bonfire.transform.position = new Vector3(-3f, 0.5f, 3f);
            bonfire.transform.localScale = new Vector3(1.2f, 0.25f, 1.2f);

            var col = bonfire.GetComponent<Collider>();
            if (col != null)
                Object.DestroyImmediate(col);

            var triggerGo = new GameObject("BonfireTrigger");
            triggerGo.transform.SetParent(bonfire.transform, false);
            triggerGo.transform.localPosition = Vector3.zero;
            var trigger = triggerGo.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 2f;

            var cue = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            cue.name = "InteractCue";
            cue.transform.SetParent(bonfire.transform, false);
            cue.transform.localPosition = new Vector3(0f, 1.4f, 0f);
            cue.transform.localScale = Vector3.one * 0.35f;
            var cueCol = cue.GetComponent<Collider>();
            if (cueCol != null)
                Object.DestroyImmediate(cueCol);
            var cueRenderer = cue.GetComponent<Renderer>();
            if (cueRenderer != null)
                cueRenderer.sharedMaterial.color = new Color(1f, 0.55f, 0.15f, 1f);

            var interact = bonfire.AddComponent<BonfireInteractable>();
            var ui = Object.FindFirstObjectByType<BonfireUIController>();
            var so = new SerializedObject(interact);
            so.FindProperty("bonfireId").stringValue = "bonfire_bayou_01";
            so.FindProperty("displayName").stringValue = "Bayou Bonfire";
            so.FindProperty("bonfireUi").objectReferenceValue = ui;
            so.FindProperty("visualCue").objectReferenceValue = cue;
            so.ApplyModifiedPropertiesWithoutUndo();
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

        private static void StretchTop(RectTransform rt, float height)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(0f, height);
            rt.anchoredPosition = Vector2.zero;
        }

        private static void StretchBottom(RectTransform rt, float height)
        {
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(-32f, height);
            rt.anchoredPosition = new Vector2(0f, 48f);
        }

        private static TextMeshProUGUI CreateTmp(string name, Transform parent, string text, float size, TextAlignmentOptions align)
        {
            var go = CreateRect(name, parent);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.alignment = align;
            tmp.color = new Color(0.95f, 0.88f, 0.72f, 1f);
            return tmp;
        }

        private static Button CreateButton(string name, Transform parent, string label)
        {
            var go = CreateRect(name, parent);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0f, 32f);

            var img = go.AddComponent<Image>();
            img.color = new Color(0.22f, 0.24f, 0.28f, 1f);

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;

            var text = CreateTmp("Text", go.transform, label, 16, TextAlignmentOptions.Center);
            StretchFull(text.rectTransform);

            return btn;
        }
    }
}
#endif
