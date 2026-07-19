using Bayou.Inventory;
using Bayou.Inventory.Shop;
using Bayou.Inventory.UI;
using Bayou.Save;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Bayou.Testing
{
    /// <summary>
    /// Dev shortcuts and on-screen guide for testing inventory, shop, and bonfire saves in Play mode.
    /// Uses ` and Shift+1–9 (F-keys are eaten by the Unity Editor). HUD buttons always work.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlaytestHarness : MonoBehaviour
    {
        [Header("Enable")]
        [SerializeField] private bool enableInPlayMode = true;
        [SerializeField] private bool showHud = true;
        [SerializeField] private bool grantStarterFishOnPlay = true;
        [SerializeField] private int starterFishCount = 2;

        [Header("References (auto-filled by Bayou/Test setup menu)")]
        [SerializeField] private ItemDefinition testFishItem;
        [SerializeField] private ShopDefinition testShop;
        [SerializeField] private string testBonfireId = "bonfire_bayou_01";
        [SerializeField] private string testBonfireName = "Bayou Bonfire";
        [SerializeField] private Transform shopTeleportPoint;
        [SerializeField] private Transform bonfireTeleportPoint;

        private InventoryController _inventory;
        private PlayerWallet _wallet;
        private ShopUIController _shopUi;
        private BonfireUIController _bonfireUi;
        private GameSaveSystem _saveSystem;
        private Vector2 _hudScroll;

        private void OnEnable()
        {
#if ENABLE_INPUT_SYSTEM
            EnsureKeyboardDevice();
#endif
        }

        private void Start()
        {
            if (!enableInPlayMode) return;

            RefreshReferences();

            // Grant starter fish after a frame so inventory UI can open/layout first if needed.
            if (grantStarterFishOnPlay && _inventory != null && _inventory.GetFishItems().Count == 0)
                StartCoroutine(GrantStarterFishNextFrame());
        }

        private System.Collections.IEnumerator GrantStarterFishNextFrame()
        {
            yield return null;
            var invUi = InventoryDisplayUI.Active ?? FindFirstObjectByType<InventoryDisplayUI>();
            invUi?.Open();
            for (var i = 0; i < starterFishCount; i++)
                AddFish();
        }

        private void Update()
        {
            if (!enableInPlayMode) return;

            RefreshReferencesIfNeeded();
            HandleShortcuts();
        }

        private void HandleShortcuts()
        {
#if ENABLE_INPUT_SYSTEM
            EnsureKeyboardDevice();
            var kb = Keyboard.current;
            if (kb == null) return;
#endif
            // ` toggles panel. Shift+1–9 are playtest actions (F-keys are captured by Unity Editor).
            if (WasBackquotePressed()) showHud = !showHud;
            if (!IsShiftHeld()) return;

            if (WasDigitPressed(1)) AddFish();
            else if (WasDigitPressed(2)) AddMoney(100);
            else if (WasDigitPressed(3)) TeleportTo(shopTeleportPoint);
            else if (WasDigitPressed(4)) TeleportTo(bonfireTeleportPoint);
            else if (WasDigitPressed(5)) ForceOpenShop();
            else if (WasDigitPressed(6)) ForceOpenBonfire();
            else if (WasDigitPressed(7)) DeleteSave();
            else if (WasDigitPressed(8)) LoadSave();
            else if (WasDigitPressed(9)) ToggleInventory();
        }

        private void OnGUI()
        {
            if (!enableInPlayMode || !showHud) return;

            // Keep HUD off the inventory grid so uGUI drag/raycasts are never blocked by IMGUI.
            var area = new Rect(Screen.width - 472f, 12f, 460f, 420f);
            var current = Event.current;
            if (current != null &&
                (current.type == EventType.MouseDown || current.type == EventType.MouseDrag ||
                 current.type == EventType.MouseUp) &&
                !area.Contains(current.mousePosition))
                return;

            GUILayout.BeginArea(area, GUI.skin.box);

            GUILayout.Label("Bayou Playtest");
            GUILayout.Label(StatusLine());
            GUILayout.Space(4f);
            GUILayout.Label("Gameplay:  I inventory  |  E interact  |  R rotate item");
            GUILayout.Label("Keys:  ` hide panel  |  Shift+1..9 shortcuts below");
            GUILayout.Space(6f);

            _hudScroll = GUILayout.BeginScrollView(_hudScroll, GUILayout.Height(260f));
            DrawActionButton("Shift+1 — Add fish", AddFish);
            DrawActionButton("Shift+2 — Add $100", () => AddMoney(100));
            DrawActionButton("Shift+3 — Go to shop", () => TeleportTo(shopTeleportPoint));
            DrawActionButton("Shift+4 — Go to bonfire", () => TeleportTo(bonfireTeleportPoint));
            DrawActionButton("Shift+5 — Open shop UI", ForceOpenShop);
            DrawActionButton("Shift+6 — Open bonfire UI", ForceOpenBonfire);
            DrawActionButton("Shift+7 — Delete save", DeleteSave);
            DrawActionButton("Shift+8 — Reload save", LoadSave);
            DrawActionButton("Shift+9 — Toggle inventory", ToggleInventory);
            GUILayout.EndScrollView();

            if (_saveSystem != null)
                GUILayout.Label($"Save: {GameSaveSystem.SaveFilePath}");

            GUILayout.EndArea();
        }

        private static void DrawActionButton(string label, System.Action action)
        {
            if (GUILayout.Button(label, GUILayout.Height(24f)))
                action?.Invoke();
        }

        private void RefreshReferences()
        {
            _inventory = InventoryController.Instance;
            _wallet = PlayerWallet.Instance;
            _shopUi = FindFirstObjectByType<ShopUIController>();
            _bonfireUi = FindFirstObjectByType<BonfireUIController>();
            _saveSystem = GameSaveSystem.Instance;
        }

        private void RefreshReferencesIfNeeded()
        {
            if (_inventory == null || _wallet == null || _shopUi == null || _bonfireUi == null || _saveSystem == null)
                RefreshReferences();
        }

        private string StatusLine()
        {
            var fish = _inventory != null ? _inventory.GetFishItems().Count : 0;
            var money = _wallet != null ? _wallet.Balance : 0;
            var hasSave = _saveSystem != null && _saveSystem.HasSaveFile;
            return $"Fish: {fish}   Money: ${money}   Save: {(hasSave ? "yes" : "no")}";
        }

        public void AddFish()
        {
            Debug.Log($"Test fish reference: {testFishItem}");

            var fish = ResolveFishItem();
            if (fish == null)
            {
                Debug.LogWarning("[Playtest] No fish item available.");
                return;
            }

            if (_inventory == null)
            {
                Debug.LogWarning("[Playtest] No InventoryController found.");
                return;
            }

            if (!_inventory.TryAddItem(fish))
                Debug.LogWarning("[Playtest] Could not add fish item to inventory.");
        }

        public void AddMoney(int amount)
        {
            if (_wallet == null)
            {
                Debug.LogWarning("[Playtest] No PlayerWallet found.");
                return;
            }

            _wallet.Add(amount);
        }

        public void TeleportTo(Transform point)
        {
            if (point == null)
            {
                Debug.LogWarning("[Playtest] Teleport point not assigned. Run Bayou/Test/Setup Playtest Scene.");
                return;
            }

            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null)
            {
                Debug.LogWarning("[Playtest] Player not found (needs Player tag).");
                return;
            }

            player.transform.SetPositionAndRotation(
                point.position,
                Quaternion.LookRotation(point.forward, Vector3.up));
        }

        public void ForceOpenShop()
        {
            RefreshReferences();

            var shop = testShop;
            if (shop == null)
            {
                var keeper = FindFirstObjectByType<Shopkeeper>();
                shop = keeper != null ? keeper.ShopDefinition : null;
            }

            // Build MockUI-styled shop at runtime if the scene has none (InventoryTest).
            _shopUi = ShopUiBuilder.EnsureInScene(shop);
            if (_shopUi == null)
            {
                Debug.LogWarning("[Playtest] Could not create Shop UI.");
                return;
            }

            if (shop == null)
                shop = _shopUi.ShopDefinition;

            if (FindFirstObjectByType<PlayerWallet>() == null && _inventory != null)
                _inventory.gameObject.AddComponent<PlayerWallet>();

            _shopUi.OpenShop(shop);
        }

        public void ForceOpenBonfire()
        {
            if (_bonfireUi == null)
            {
                Debug.LogWarning("[Playtest] Bonfire UI missing. Run Bayou/Test/Setup Playtest Scene.");
                return;
            }

            _bonfireUi.Open(testBonfireId, testBonfireName);
        }

        public void DeleteSave()
        {
            if (_saveSystem == null)
            {
                Debug.LogWarning("[Playtest] GameSaveSystem missing.");
                return;
            }

            _saveSystem.DeleteSave();
        }

        public void LoadSave()
        {
            if (_saveSystem == null)
            {
                Debug.LogWarning("[Playtest] GameSaveSystem missing.");
                return;
            }

            if (!_saveSystem.Load())
                Debug.LogWarning("[Playtest] No save file to load.");
        }

        public void ToggleInventory()
        {
            var handmade = InventoryDisplayUI.Active ?? FindFirstObjectByType<InventoryDisplayUI>();
            if (handmade != null)
            {
                if (handmade.IsLockedByShop)
                {
                    Debug.Log("[Playtest] Inventory is locked while the shop is open. Use Close Deal or Cancel.");
                    return;
                }

                handmade.Toggle();
                return;
            }

            var ui = FindFirstObjectByType<InventoryUIController>();
            if (ui == null)
            {
                Debug.LogWarning("[Playtest] Inventory UI missing.");
                return;
            }

            ui.Toggle();
        }

        private ItemDefinition ResolveFishItem()
        {
            if (testFishItem != null)
                return testFishItem;

            var catalog = _saveSystem?.ItemCatalog;
            if (catalog != null)
            {
                foreach (var item in catalog.AllDefinitions)
                {
                    if (item != null && item.isFish)
                        return item;
                }
            }

            return null;
        }

#if ENABLE_INPUT_SYSTEM
        private static void EnsureKeyboardDevice()
        {
            if (Keyboard.current != null) return;

            foreach (var device in InputSystem.devices)
            {
                if (device is Keyboard)
                    return;
            }

            InputSystem.AddDevice<Keyboard>();
        }

        private static bool IsShiftHeld()
        {
            var kb = Keyboard.current;
            return kb != null && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed);
        }

        private static bool WasBackquotePressed()
        {
            var kb = Keyboard.current;
            return kb != null && kb.backquoteKey.wasPressedThisFrame;
        }

        private static bool WasDigitPressed(int digit)
        {
            if (digit < 1 || digit > 9) return false;

            var kb = Keyboard.current;
            if (kb == null) return false;

            var key = (Key)((int)Key.Digit1 + (digit - 1));
            return kb[key].wasPressedThisFrame;
        }
#else
        private static void EnsureKeyboardDevice() { }

        private static bool IsShiftHeld() => false;

        private static bool WasBackquotePressed() => false;

        private static bool WasDigitPressed(int digit) => false;
#endif
    }
}
