using Bayou.Fish;
using Bayou.Fishing;
using Bayou.Inventory;
using Bayou.Inventory.Shop;
using Bayou.Quests;
using Bayou.Save;
using Bayou.UI;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Bayou.Testing
{
    /// <summary>
    /// Play-mode HUD for testing fishing, the pond rosary quest, lantern, and travel.
    /// In the Editor it auto-spawns when you press Play. Toggle with backtick (`).
    /// </summary>
    [DefaultExecutionOrder(-150)]
    [DisallowMultipleComponent]
    public sealed class MechanicsTestBootstrap : MonoBehaviour
    {
        [SerializeField] private bool skipSaveOnPlay = true;
        [Tooltip("Leave off so Play matches a real start ($0, no tools). Use the HUD to grant test items.")]
        [SerializeField] private bool grantRodOnPlay;
        [SerializeField] private bool grantNetOnPlay;
        [SerializeField] private bool grantMoneyOnPlay;
        [SerializeField] private int startingMoney = 150;
        [SerializeField] private bool showHud = true;

        private Vector2 _scroll;
        private bool _skippedSaveThisPlay;

        public static MechanicsTestBootstrap Instance { get; private set; }

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoSpawnInEditor()
        {
            if (FindFirstObjectByType<MechanicsTestBootstrap>() != null)
                return;

            var go = new GameObject("MechanicsTestBootstrap");
            go.AddComponent<MechanicsTestBootstrap>();
        }
#endif

        public static void EnsureInScene()
        {
            if (FindFirstObjectByType<MechanicsTestBootstrap>() != null)
                return;
            var go = new GameObject("MechanicsTestBootstrap");
            go.AddComponent<MechanicsTestBootstrap>();
        }

        private void Awake()
        {
            Instance = this;
            if (skipSaveOnPlay)
            {
                GameSaveSystem.SuppressNextLoad = true;
                _skippedSaveThisPlay = true;
            }
        }

        private void Start()
        {
            StartCoroutine(GrantDefaultsNextFrame());
        }

        private System.Collections.IEnumerator GrantDefaultsNextFrame()
        {
            yield return null;
            if (grantMoneyOnPlay)
                AddMoney(startingMoney);
            if (grantNetOnPlay)
            {
                GrantItem("Item_HandNet");
                Equip(BayouHeldItem.Net);
            }
            if (grantRodOnPlay)
                GrantItem("Item_FishingRod");
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void Update()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null && kb.backquoteKey.wasPressedThisFrame)
                showHud = !showHud;
#else
            if (Input.GetKeyDown(KeyCode.BackQuote))
                showHud = !showHud;
#endif
        }

        private void OnGUI()
        {
            if (!showHud) return;

            var w = 300f;
            var h = Mathf.Min(Screen.height - 24f, 620f);
            GUILayout.BeginArea(new Rect(12f, 12f, w, h), GUI.skin.box);
            GUILayout.Label("Mechanics bootstrap");
            GUILayout.Label("` hide  ·  Tab hold wheel  ·  1–4 slots");
            GUILayout.Label("Net: hold LMB over pond glow, release");
            GUILayout.Label("Rod: hold LMB, release to cast, A/D wiggle");
            GUILayout.Space(4f);

            _scroll = GUILayout.BeginScrollView(_scroll);

            GUILayout.Label("Hotwheel");
            Btn("Hotwheel kit + open wheel", SetupHotwheelTest);
            Btn("Open wheel", OpenHotwheel);
            Btn("Close wheel", CloseHotwheel);
            Btn("Equip slot 1", () => SelectHotwheelSlot(0));
            Btn("Equip slot 2", () => SelectHotwheelSlot(1));
            Btn("Equip slot 3", () => SelectHotwheelSlot(2));
            Btn("Equip slot 4", () => SelectHotwheelSlot(3));
            Btn("Clear wheel slots", ClearHotwheelSlots);

            GUILayout.Space(6f);
            GUILayout.Label("Fishing");
            Btn("Rod fishing", StartRodFishing);
            Btn("Church pond (rosary)", GoPond);

            GUILayout.Space(6f);
            GUILayout.Label("Go to");
            Btn("Father Landry (church)", GoLandry);
            Btn("Player spawn", GoSpawn);
            Btn("Caliste", GoCaliste);
            Btn("Shop", GoShop);
            Btn("Lantern pickup", GoLantern);

            GUILayout.Space(6f);
            GUILayout.Label("Give / equip");
            Btn("Give test kit ($150 + net + rod)", GiveTestKit);
            Btn("Give net + equip", () =>
            {
                GrantItem("Item_HandNet");
                Equip(BayouHeldItem.Net);
            });
            Btn("Give rod + equip", () =>
            {
                GrantItem("Item_FishingRod");
                Equip(BayouHeldItem.Rod);
            });
            Btn("Give lantern + equip", () =>
            {
                GrantItem("Item_Lantern");
                Equip(BayouHeldItem.Lantern);
            });
            Btn("Give rosary (skip scoop)", () => GrantItem("Item_RosaryNecklace"));
            Btn("Give graveyard key", () =>
            {
                GrantItem("Item_ChurchGraveyardKey");
                var gates = KeyGateManager.Instance ?? FindFirstObjectByType<KeyGateManager>();
                gates?.SyncKeysFromInventory();
            });
            Btn("Give $100", () => AddMoney(100));

            GUILayout.Space(6f);
            GUILayout.Label("Shop stock");
            Btn("Add all catalog to shop", AddAllCatalogToShop);
            var catalog = ResolveCatalog();
            if (catalog == null)
            {
                GUILayout.Label("(no ItemCatalog)");
            }
            else
            {
                foreach (var item in catalog.AllDefinitions)
                {
                    if (item == null) continue;
                    var name = string.IsNullOrWhiteSpace(item.displayName) ? item.name : item.displayName;
                    Btn($"+ shop {name}", () => AddItemToShop(item));
                }
            }

            GUILayout.Space(6f);
            GUILayout.Label("Quests");
            Btn("Start pond / rosary quest", () => StartQuest("CollectPondItemQuest"));
            Btn("Start lantern quest", () => StartQuest("CollectLanternQuest"));

            GUILayout.Space(6f);
            GUILayout.Label("Save");
            Btn("Delete save file", DeleteSave);
            GUILayout.Label(_skippedSaveThisPlay
                ? "This Play: save load skipped"
                : "This Play: save may load");

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private static void Btn(string label, System.Action action)
        {
            if (GUILayout.Button(label, GUILayout.Height(26f)))
                action?.Invoke();
        }

        private static void StartRodFishing()
        {
            GrantItem("Item_FishingRod");
            Equip(BayouHeldItem.Rod);

            if (TryFindRodFish(out var water))
            {
                var stand = water + new Vector3(5.8f, 1.55f, 2.2f);
                Teleport(stand, water - stand);
                Debug.Log("[Mechanics] Rod fishing: hold LMB, release to cast, then wiggle A/D.");
                return;
            }

            var pond = GameObject.Find("Graveyard_TreePond");
            if (pond != null)
            {
                var waterPos = pond.transform.position;
                var stand = waterPos + new Vector3(6.2f, 1.55f, 2f);
                Teleport(stand, waterPos - stand);
                Debug.Log("[Mechanics] Rod fishing at Graveyard_TreePond. Hold LMB, release to cast, A/D wiggle.");
                return;
            }

            Teleport(new Vector3(-146f, 1.6f, 32f), new Vector3(-1f, 0f, -0.3f));
            Debug.Log("[Mechanics] Rod fishing fallback. Hold LMB, release to cast, A/D wiggle.");
        }

        private static bool TryFindRodFish(out Vector3 water)
        {
            water = default;
            var fish = Object.FindObjectsByType<BayouFish>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var f in fish)
            {
                if (f == null || f.IsCaught || !f.CanCatchWith(FishCatchTool.Rod))
                    continue;
                water = f.transform.position;
                return true;
            }

            foreach (var spot in FishingSpot.AllSpots)
            {
                if (spot == null) continue;
                if (spot.RequiredTool == FishCatchTool.Rod)
                {
                    water = spot.transform.position;
                    return true;
                }

                foreach (var spawned in spot.SpawnedFish)
                {
                    if (spawned == null || spawned.IsCaught || !spawned.CanCatchWith(FishCatchTool.Rod))
                        continue;
                    water = spawned.transform.position;
                    return true;
                }
            }

            return false;
        }

        private static void GoPond()
        {
            var shiny = FindFirstObjectByType<PondShinyCollectible>();
            if (shiny != null)
            {
                var p = shiny.transform.position + new Vector3(3.2f, 1.5f, 2.2f);
                Teleport(p, shiny.transform.position - p);
                return;
            }

            Teleport(new Vector3(-14f, 1.6f, -40f), Vector3.forward);
        }

        private static void GoLandry()
        {
            var npc = GameObject.Find("Church NPC");
            if (npc == null) npc = GameObject.Find("Zenon Landry");
            if (npc == null) npc = GameObject.Find("Father Landry");
            if (npc == null)
            {
                Debug.LogWarning("[Mechanics] Church NPC not found.");
                return;
            }

            var stand = npc.transform.position + new Vector3(2f, 0f, 1.2f);
            stand.y = Mathf.Max(npc.transform.position.y + 1.1f, 1.4f);
            Teleport(stand, npc.transform.position - stand);
        }

        private static void GoSpawn()
        {
            Teleport(new Vector3(-10.06f, 1.6f, -92.71f), Vector3.forward);
        }

        private static void GoCaliste()
        {
            var go = GameObject.Find("Caliste");
            if (go == null) go = GameObject.Find("Caliste NPC");
            if (go == null)
            {
                Teleport(new Vector3(-81.5f, 1.6f, 93.4f), Vector3.forward);
                return;
            }

            var stand = go.transform.position + new Vector3(2.2f, 0f, 0f);
            stand.y = Mathf.Max(go.transform.position.y + 1.1f, 1.2f);
            Teleport(stand, go.transform.position - stand);
        }

        private static void GoShop()
        {
            Transform dest = null;
            var shopPoint = GameObject.Find("ShopPoint");
            if (shopPoint != null)
                dest = shopPoint.transform;

            if (dest == null)
            {
                var keeper = FindFirstObjectByType<Shopkeeper>();
                if (keeper != null)
                    dest = keeper.transform;
            }

            if (dest == null)
            {
                var caliste = GameObject.Find("Caliste");
                if (caliste == null) caliste = GameObject.Find("Caliste NPC");
                if (caliste != null)
                    dest = caliste.transform;
            }

            if (dest != null)
            {
                var stand = dest.position + new Vector3(2.2f, 0f, 0f);
                stand.y = Mathf.Max(dest.position.y + 1.1f, 1.2f);
                Teleport(stand, dest.position - stand);
            }
            else
            {
                Teleport(new Vector3(-81.5f, 1.6f, 93.4f), Vector3.forward);
            }

            OpenShopUi();
        }

        private static ShopUIController OpenShopUi()
        {
            var keeper = FindFirstObjectByType<Shopkeeper>();
            var shop = keeper != null ? keeper.ShopDefinition : null;
            var shopUi = ShopUiBuilder.EnsureInScene(shop);
            if (shopUi == null)
            {
                Debug.LogWarning("[Mechanics] Could not open shop UI.");
                return null;
            }

            if (shop == null)
                shop = shopUi.ShopDefinition;

            var handmade = InventoryDisplayUI.Active ?? FindFirstObjectByType<InventoryDisplayUI>();
            if (handmade != null)
                shopUi.AssignHandmadeInventory(handmade);

            if (!shopUi.IsOpen)
                shopUi.OpenShop(shop);

            return shopUi;
        }

        private static ItemCatalog ResolveCatalog()
        {
            var catalog = GameSaveSystem.Instance != null ? GameSaveSystem.Instance.ItemCatalog : null;
            return catalog != null ? catalog : Resources.Load<ItemCatalog>("Bayou/ItemCatalog");
        }

        private static void AddItemToShop(ItemDefinition item)
        {
            if (item == null) return;
            var shopUi = OpenShopUi();
            if (shopUi == null) return;
            if (shopUi.TryAddPlaytestStock(item))
                Debug.Log($"[Mechanics] Added {item.displayName} to shop stock.");
        }

        private static void AddAllCatalogToShop()
        {
            var catalog = ResolveCatalog();
            if (catalog == null)
            {
                Debug.LogWarning("[Mechanics] ItemCatalog missing.");
                return;
            }

            var shopUi = OpenShopUi();
            if (shopUi == null) return;

            var added = 0;
            foreach (var item in catalog.AllDefinitions)
            {
                if (item == null) continue;
                if (shopUi.TryAddPlaytestStock(item))
                    added++;
            }

            Debug.Log($"[Mechanics] Added {added} catalog item(s) to shop stock.");
        }

        private static void GoLantern()
        {
            var pickup = GameObject.Find("LanternPickup");
            if (pickup == null)
            {
                Debug.LogWarning("[Mechanics] LanternPickup not found.");
                return;
            }

            var stand = pickup.transform.position + new Vector3(2f, 1.4f, 0f);
            Teleport(stand, pickup.transform.position - stand);
        }

        private static void GiveTestKit()
        {
            AddMoney(150);
            GrantItem("Item_HandNet");
            GrantItem("Item_FishingRod");
            Equip(BayouHeldItem.Net);
            Debug.Log("[Mechanics] Test kit: $150, hand net (equipped), fishing rod.");
        }

        private static void SetupHotwheelTest()
        {
            GrantItem("Item_FishingRod");
            GrantItem("Item_HandNet");
            GrantItem("Item_Lantern");

            var wheel = ResolveHotwheel();
            if (wheel == null)
            {
                Debug.LogWarning("[Mechanics] EquipmentHotwheel missing.");
                return;
            }

            wheel.SetSlotItemIds("Item_FishingRod", "Item_HandNet", "Item_Lantern");
            wheel.OpenWheel();
            Debug.Log("[Mechanics] Hotwheel kit: rod / net / lantern in slots 1–3. Aim a slice, or use 1–4. Close wheel or tap Tab to dismiss.");
        }

        private static void OpenHotwheel()
        {
            var wheel = ResolveHotwheel();
            if (wheel == null)
            {
                Debug.LogWarning("[Mechanics] EquipmentHotwheel missing.");
                return;
            }

            wheel.OpenWheel();
        }

        private static void CloseHotwheel()
        {
            var wheel = EquipmentHotwheel.Instance ?? FindFirstObjectByType<EquipmentHotwheel>();
            wheel?.CloseWheel();
        }

        private static void SelectHotwheelSlot(int index)
        {
            var wheel = ResolveHotwheel();
            if (wheel == null) return;
            if (!wheel.TrySelectSlot(index))
                Debug.Log($"[Mechanics] Hotwheel slot {index + 1} is empty or you don't have that item.");
        }

        private static void ClearHotwheelSlots()
        {
            var wheel = ResolveHotwheel();
            if (wheel == null) return;
            wheel.SetSlotItemIds();
            Debug.Log("[Mechanics] Hotwheel slots cleared.");
        }

        private static EquipmentHotwheel ResolveHotwheel()
        {
            EquipmentHotwheel.EnsureInScene();
            return EquipmentHotwheel.Instance ?? FindFirstObjectByType<EquipmentHotwheel>(FindObjectsInactive.Include);
        }

        private static void GrantItem(string itemId)
        {
            var inv = InventoryController.Instance ?? FindFirstObjectByType<InventoryController>();
            if (inv == null)
            {
                Debug.LogWarning("[Mechanics] No inventory.");
                return;
            }

            var def = ResolveItem(itemId);
            if (def == null)
            {
                Debug.LogWarning($"[Mechanics] Missing item '{itemId}'.");
                return;
            }

            if (inv.HasItemsById(itemId, 1))
            {
                Debug.Log($"[Mechanics] Already have {def.displayName}.");
                return;
            }

            if (!inv.TryAddItem(def) && !inv.TryHoldNewItem(def, out _))
                Debug.LogWarning($"[Mechanics] Could not add {def.displayName} — bag full?");
            else
                Debug.Log($"[Mechanics] Granted {def.displayName}.");
        }

        private static ItemDefinition ResolveItem(string itemId)
        {
            var catalog = GameSaveSystem.Instance != null ? GameSaveSystem.Instance.ItemCatalog : null;
            catalog ??= Resources.Load<ItemCatalog>("Bayou/ItemCatalog");
            if (catalog != null)
            {
                var fromCat = catalog.Resolve(itemId);
                if (fromCat != null) return fromCat;
            }

            foreach (var def in Resources.LoadAll<ItemDefinition>("Bayou/Items"))
            {
                if (def != null && def.MatchesId(itemId))
                    return def;
            }

            return null;
        }

        private static void Equip(BayouHeldItem item)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            var equipment = player != null
                ? player.GetComponent<BayouFishingEquipment>()
                : FindFirstObjectByType<BayouFishingEquipment>();
            if (equipment == null)
            {
                Debug.LogWarning("[Mechanics] No BayouFishingEquipment.");
                return;
            }

            equipment.ApplyItem(item);
        }

        private static void StartQuest(string questId)
        {
            var manager = QuestManager.Resolve();
            if (manager == null)
            {
                Debug.LogWarning("[Mechanics] No QuestManager.");
                return;
            }

            manager.StartQuest(questId);
            Debug.Log($"[Mechanics] Started {questId}.");
        }

        private static void AddMoney(int amount)
        {
            var wallet = PlayerWallet.Instance ?? FindFirstObjectByType<PlayerWallet>();
            if (wallet == null)
            {
                Debug.LogWarning("[Mechanics] No PlayerWallet.");
                return;
            }

            wallet.Add(amount);
        }

        private static void DeleteSave()
        {
            if (!System.IO.File.Exists(GameSaveSystem.SaveFilePath))
            {
                Debug.Log("[Mechanics] No save file.");
                return;
            }

            System.IO.File.Delete(GameSaveSystem.SaveFilePath);
            Debug.Log($"[Mechanics] Deleted {GameSaveSystem.SaveFilePath}");
        }

        private static void Teleport(Vector3 position, Vector3 faceToward)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null)
            {
                Debug.LogWarning("[Mechanics] Player not tagged.");
                return;
            }

            if (faceToward.sqrMagnitude < 0.01f)
                faceToward = Vector3.forward;
            faceToward.y = 0f;
            var rot = Quaternion.LookRotation(faceToward.normalized, Vector3.up);

            var rb = player.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.position = position;
                rb.rotation = rot;
            }

            player.transform.SetPositionAndRotation(position, rot);
            Debug.Log($"[Mechanics] Teleported to {position}");
        }
    }
}
