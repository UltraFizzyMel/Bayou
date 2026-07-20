#if UNITY_EDITOR
using Bayou.Audio.Editor;
using Bayou.Inventory;
using Bayou.Inventory.Shop;
using Bayou.Save;
using Bayou.Testing;
using Bayou.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace Bayou.Inventory.Editor
{
    public static class PlaytestSetupMenu
    {
        private const string PlaytestRootName = "PlaytestHub";

        [MenuItem("Bayou/Test/Setup Playtest Scene (Inventory + Shop + Bonfire)", false, 0)]
        public static void SetupPlaytestScene()
        {
            SetupPlaytestScene(preservePlayerPosition: false);
        }

        /// <summary>
        /// Wires inventory, shop, bonfire, UI input, and playtest harness into the open scene.
        /// When <paramref name="preservePlayerPosition"/> is true (MovementTest), shop/bonfire
        /// are placed near the existing player instead of relocating them to the origin.
        /// </summary>
        public static void SetupPlaytestScene(bool preservePlayerPosition)
        {
            // Tag first — inventory/shop setup look up Player by tag and must not spawn a duplicate.
            TagPlayer();

            InventorySetupMenu.CreateSampleItems();
            InventorySetupMenu.CreateCaseLayoutAsset();

            // Handmade MockUI bag (InventoryTest), not procedural InventoryUIController.
            HandmadeInventorySetupMenu.EnsureHandmadeInventoryInScene(removeProceduralInventory: true);

            ShopSetupMenu.SetupShopInScene(forceRecreate: true);
            HandmadeInventorySetupMenu.WireShopToHandmade();

            BonfireSetupMenu.SetupBonfireInScene();
            BonfireSetupMenu.CreateOrRefreshItemCatalog();

            EnsurePlayerWallet();
            EnsureUiInput();
            InventoryInputWiring.WireInventoryActionsInScene();
            EnsureGameplayUiLayout();
            EnsurePlaytestLayout(preservePlayerPosition);
            EnsurePlaytestHarness();
            HandmadeInventorySetupMenu.EnsurePlaytestHarnessRefs();
            WireCaughtFishItems();
            AudioSetupMenu.WireGameplayAudio();

            Debug.Log(
                "[Bayou] Playtest scene ready (handmade MockUI inventory).\n" +
                "  I = bag | E = interact | drag+R = rotate | Shift+1..9 = playtest (` = HUD)");
        }

        [MenuItem("Bayou/Test/Delete Save File")]
        public static void DeleteSaveFileMenu()
        {
            if (!System.IO.File.Exists(GameSaveSystem.SaveFilePath))
            {
                Debug.Log("[Bayou] No save file found.");
                return;
            }

            System.IO.File.Delete(GameSaveSystem.SaveFilePath);
            Debug.Log($"[Bayou] Deleted save: {GameSaveSystem.SaveFilePath}");
        }

        [MenuItem("Bayou/Test/Open Save Folder")]
        public static void OpenSaveFolder()
        {
            EditorUtility.RevealInFinder(Application.persistentDataPath);
        }

        private static void TagPlayer()
        {
            var player = FindPlayerObject();
            if (player == null)
            {
                Debug.LogWarning("[Bayou] No Player object found — create/tag a Player for interact tests.");
                return;
            }

            if (!player.CompareTag("Player"))
            {
                player.tag = "Player";
                EditorUtility.SetDirty(player);
            }
        }

        private static void EnsurePlayerWallet()
        {
            var player = FindPlayerObject();
            if (player != null && player.GetComponent<PlayerWallet>() == null)
                Undo.AddComponent<PlayerWallet>(player);
        }

        private static void EnsureUiInput()
        {
            var eventSystem = Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();
            if (eventSystem == null)
            {
                Debug.LogWarning("[Bayou] No EventSystem in scene — UI clicks will not work.");
                return;
            }

            var module = eventSystem.GetComponent<InputSystemUIInputModule>();
            if (module == null)
                module = Undo.AddComponent<InputSystemUIInputModule>(eventSystem.gameObject);

            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
            if (actions != null && module.actionsAsset == null)
            {
                var moduleSo = new SerializedObject(module);
                moduleSo.FindProperty("m_ActionsAsset").objectReferenceValue = actions;
                moduleSo.ApplyModifiedPropertiesWithoutUndo();
            }

            var bootstrap = eventSystem.GetComponent<BayouUiInputBootstrap>();
            if (bootstrap == null)
                bootstrap = Undo.AddComponent<BayouUiInputBootstrap>(eventSystem.gameObject);

            if (actions != null)
            {
                var bootstrapSo = new SerializedObject(bootstrap);
                bootstrapSo.FindProperty("actionsAsset").objectReferenceValue = actions;
                bootstrapSo.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void EnsureGameplayUiLayout()
        {
            if (Object.FindFirstObjectByType<GameplayUiLayout>() != null)
                return;

            var shop = Object.FindFirstObjectByType<ShopUIController>();
            if (shop != null)
            {
                shop.gameObject.AddComponent<GameplayUiLayout>();
                return;
            }

            var go = new GameObject("GameplayUiLayout");
            go.AddComponent<GameplayUiLayout>();
        }

        private static void EnsurePlaytestLayout(bool preservePlayerPosition)
        {
            var root = GameObject.Find(PlaytestRootName);
            if (root == null)
                root = new GameObject(PlaytestRootName);

            var player = FindPlayerObject();
            var origin = new Vector3(0f, 1f, 0f);
            if (player != null)
            {
                if (preservePlayerPosition)
                {
                    origin = player.transform.position;
                }
                else
                {
                    Undo.RecordObject(player.transform, "Position Player");
                    player.transform.position = origin;
                }
            }

            var shopPoint = EnsureMarker(root.transform, "ShopPoint", origin + new Vector3(6f, 0f, 0f), new Color(0.9f, 0.75f, 0.2f));
            var bonfirePoint = EnsureMarker(root.transform, "BonfirePoint", origin + new Vector3(-6f, 0f, 0f), new Color(1f, 0.45f, 0.1f));

            MoveOrCreateShopkeeper(shopPoint);
            MoveOrCreateBonfire(bonfirePoint);

            Selection.activeGameObject = root;
        }

        private static void WireCaughtFishItems()
        {
            var fishItem = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Inventory/Items/Item_Fish.asset");
            if (fishItem == null) return;

            var fish = Object.FindObjectsByType<Bayou.Fish.BayouFish>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var f in fish)
            {
                var so = new SerializedObject(f);
                var prop = so.FindProperty("inventoryItemWhenCaught");
                if (prop == null || prop.objectReferenceValue != null) continue;
                prop.objectReferenceValue = fishItem;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(f);
            }
        }

        private static Transform EnsureMarker(Transform parent, string name, Vector3 position, Color color)
        {
            var existing = parent.Find(name);
            GameObject go;
            if (existing != null)
            {
                go = existing.gameObject;
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                go.name = name;
                go.transform.SetParent(parent, false);
                var col = go.GetComponent<Collider>();
                if (col != null)
                    Object.DestroyImmediate(col);
            }

            go.transform.position = position;
            go.transform.localScale = new Vector3(0.6f, 0.05f, 0.6f);
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
                renderer.sharedMaterial.color = color;

            return go.transform;
        }

        private static void MoveOrCreateShopkeeper(Transform shopPoint)
        {
            var keeper = Object.FindFirstObjectByType<Shopkeeper>();
            if (keeper != null)
            {
                Undo.RecordObject(keeper.transform, "Move Shopkeeper");
                keeper.transform.position = shopPoint.position + Vector3.back * 1.5f;
                return;
            }

            Debug.LogWarning("[Bayou] Shopkeeper missing — run Bayou/Shop/Setup Shop UI In Scene.");
        }

        private static void MoveOrCreateBonfire(Transform bonfirePoint)
        {
            var bonfire = Object.FindFirstObjectByType<BonfireInteractable>();
            if (bonfire != null)
            {
                Undo.RecordObject(bonfire.transform, "Move Bonfire");
                bonfire.transform.position = bonfirePoint.position + Vector3.back * 1.5f;
                return;
            }

            Debug.LogWarning("[Bayou] Bonfire missing — run Bayou/Save/Setup Bonfire Save System In Scene.");
        }

        private static void EnsurePlaytestHarness()
        {
            var fish = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Inventory/Items/Item_Fish.asset");
            if (fish == null)
            {
                var guids = AssetDatabase.FindAssets("t:ItemDefinition Fish");
                if (guids.Length > 0)
                    fish = AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(guids[0]));
            }

            var shop = AssetDatabase.LoadAssetAtPath<ShopDefinition>("Assets/Inventory/Shop/Shop_BayouMerchant.asset");
            var shopPoint = GameObject.Find($"{PlaytestRootName}/ShopPoint")?.transform;
            var bonfirePoint = GameObject.Find($"{PlaytestRootName}/BonfirePoint")?.transform;

            var harness = Object.FindFirstObjectByType<PlaytestHarness>();
            if (harness == null)
            {
                var go = new GameObject("PlaytestHarness");
                harness = go.AddComponent<PlaytestHarness>();
                Undo.RegisterCreatedObjectUndo(go, "Create PlaytestHarness");
            }

            var so = new SerializedObject(harness);
            so.FindProperty("enableInPlayMode").boolValue = true;
            so.FindProperty("showHud").boolValue = true;
            so.FindProperty("grantStarterFishOnPlay").boolValue = false;
            so.FindProperty("starterFishCount").intValue = 0;
            so.FindProperty("testFishItem").objectReferenceValue = fish;
            so.FindProperty("testShop").objectReferenceValue = shop;
            so.FindProperty("shopTeleportPoint").objectReferenceValue = shopPoint;
            so.FindProperty("bonfireTeleportPoint").objectReferenceValue = bonfirePoint;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject FindPlayerObject()
        {
            var tagged = GameObject.FindGameObjectWithTag("Player");
            if (tagged != null)
                return tagged;

            var byName = GameObject.Find("Player");
            return byName;
        }
    }
}
#endif
