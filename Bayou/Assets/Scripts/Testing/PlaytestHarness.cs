using Bayou.Fishing;
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
        [SerializeField] private Transform pondTeleportPoint;
        [Tooltip("Used when pondTeleportPoint is unset (church pond / fish area).")]
        [SerializeField] private Vector3 pondTeleportFallback = new Vector3(-14f, 1.6f, -40f);
        [SerializeField] private Transform calisteTeleportPoint;
        [Tooltip("Used when calisteTeleportPoint is unset.")]
        [SerializeField] private Vector3 calisteTeleportFallback = new Vector3(-81.5f, 1.6f, 93.4f);

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
            // Spots are world content, not a playtest-only cheat.
            FishingSpotBootstrap.EnsureInScene();

            if (!enableInPlayMode) return;

            RefreshReferences();
            AudioSettings.ApplySavedVolumesIfPresent();

            // Starter fish silently — do not force inventory/shop open.
            if (grantStarterFishOnPlay && _inventory != null && _inventory.GetFishItems().Count == 0)
                StartCoroutine(GrantStarterFishNextFrame());
        }

        private System.Collections.IEnumerator GrantStarterFishNextFrame()
        {
            yield return null;
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
            // ` toggles panel. V = volume. Esc closes volume. Shift+1–9 are playtest actions.
            if (WasBackquotePressed()) showHud = !showHud;
            if (WasKeyPressed(KeyCode.V)) ToggleAudioSettings();
            if (AudioSettings.IsOpen && WasKeyPressed(KeyCode.Escape))
                AudioSettings.CloseIfOpen();

            if (!IsShiftHeld()) return;

            if (WasDigitPressed(1)) AddFish();
            else if (WasDigitPressed(2)) AddMoney(100);
            else if (WasDigitPressed(3)) TeleportToCaliste();
            else if (WasDigitPressed(4)) TeleportTo(bonfireTeleportPoint);
            else if (WasDigitPressed(5)) ToggleShop();
            else if (WasDigitPressed(6)) ForceOpenBonfire();
            else if (WasDigitPressed(7)) DeleteSave();
            else if (WasDigitPressed(8)) LoadSave();
            else if (WasDigitPressed(9)) ToggleInventory();
            else if (WasDigitPressed(0)) TeleportToPond();
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
            GUILayout.Label("Fishing: 2=net scoop  |  1=rod cast  |  Esc/Q/RMB cancel");
            GUILayout.Label("Audio:  V volume settings  |  Esc close");
            GUILayout.Label("Keys:  ` hide panel  |  Shift+3 Caliste  |  Shift+5 shop UI");
            GUILayout.Space(6f);

            _hudScroll = GUILayout.BeginScrollView(_hudScroll, GUILayout.Height(260f));
            DrawActionButton("V — Toggle volume settings", ToggleAudioSettings);
            DrawActionButton("Shift+0 — Go to church pond / fish", TeleportToPond);
            DrawActionButton("Shift+1 — Add fish", AddFish);
            DrawActionButton("Shift+2 — Add $100", () => AddMoney(100));
            DrawActionButton("Shift+3 — Go to Caliste", TeleportToCaliste);
            DrawActionButton("Shift+4 — Go to bonfire", () => TeleportTo(bonfireTeleportPoint));
            DrawActionButton("Shift+5 — Toggle shop UI", ToggleShop);
            DrawActionButton("Shift+6 — Open bonfire UI", ForceOpenBonfire);
            DrawActionButton("Shift+7 — Delete save", DeleteSave);
            DrawActionButton("Shift+8 — Reload save", LoadSave);
            DrawActionButton("Shift+9 — Toggle inventory", ToggleInventory);
            DrawActionButton("Grant Landry graveyard key", GrantLandryKey);
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

            TeleportPlayer(point.position, point.forward);
        }

        public void TeleportToPond()
        {
            if (pondTeleportPoint != null)
            {
                TeleportTo(pondTeleportPoint);
                return;
            }

            TeleportPlayer(pondTeleportFallback, Vector3.forward);
        }

        public void GrantLandryKey()
        {
            var inv = InventoryController.Instance;
            var catalog = _saveSystem != null ? _saveSystem.ItemCatalog : GameSaveSystem.Instance?.ItemCatalog;
            var key = catalog != null ? catalog.Resolve("Item_ChurchGraveyardKey") : null;
            if (key == null)
            {
                Debug.LogWarning("[Playtest] Item_ChurchGraveyardKey missing from catalog.");
                return;
            }

            if (inv == null || (!inv.TryAddItem(key) && !inv.TryHoldNewItem(key, out _)))
            {
                Debug.LogWarning("[Playtest] Could not add graveyard key.");
                return;
            }

            var gates = KeyGateManager.Instance ?? FindFirstObjectByType<KeyGateManager>();
            gates?.GrantKeyFlag("hasKeyChurchToGraveyard");
            Debug.Log("[Playtest] Granted Church Graveyard Key.");
        }

        public void TeleportToCaliste()
        {
            if (calisteTeleportPoint != null)
            {
                TeleportTo(calisteTeleportPoint);
                return;
            }

            if (TryResolveCaliste(out var calistePos, out var faceDir))
            {
                TeleportPlayer(calistePos, faceDir);
                return;
            }

            Debug.LogWarning("[Playtest] Caliste not found — using fallback position.");
            TeleportPlayer(calisteTeleportFallback, Vector3.forward);
        }

        private static bool TryResolveCaliste(out Vector3 standPos, out Vector3 faceDir)
        {
            standPos = default;
            faceDir = Vector3.forward;

            Transform caliste = null;

            var byName = GameObject.Find("Caliste");
            if (byName != null)
                caliste = byName.transform;

            if (caliste == null)
            {
                foreach (var trigger in Object.FindObjectsByType<DialogueTrigger>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                {
                    if (trigger == null) continue;
                    // Caliste dialogue asset, or object named Caliste in the hierarchy.
                    var n = trigger.gameObject.name;
                    var rootName = trigger.transform.root != null ? trigger.transform.root.name : n;
                    if (n.IndexOf("Caliste", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                        rootName.IndexOf("Caliste", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        caliste = trigger.transform.root != null ? trigger.transform.root : trigger.transform;
                        break;
                    }
                }
            }

            if (caliste == null)
                return false;

            // Stand slightly offset on XZ so we land in her interact trigger.
            var origin = caliste.position;
            standPos = origin + new Vector3(2.2f, 0f, 0f);
            standPos.y = Mathf.Max(origin.y + 1.1f, 1.2f);
            faceDir = origin - standPos;
            faceDir.y = 0f;
            if (faceDir.sqrMagnitude < 0.01f)
                faceDir = Vector3.forward;
            return true;
        }

        private static void TeleportPlayer(Vector3 position, Vector3 forward)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null)
            {
                Debug.LogWarning("[Playtest] Player not found (needs Player tag).");
                return;
            }

            if (forward.sqrMagnitude < 0.01f)
                forward = Vector3.forward;

            var rot = Quaternion.LookRotation(forward.normalized, Vector3.up);

            // Player uses a Rigidbody motor — transform-only teleports get overwritten next FixedUpdate.
            var rb = player.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.position = position;
                rb.rotation = rot;
            }

            var cc = player.GetComponent<CharacterController>();
            if (cc != null)
                cc.enabled = false;

            player.transform.SetPositionAndRotation(position, rot);

            if (cc != null)
                cc.enabled = true;

            Debug.Log($"[Playtest] Teleported player to {position}");
        }

        public void ToggleShop()
        {
            RefreshReferences();

            _shopUi ??= ShopUiBuilder.EnsureInScene(testShop);
            if (_shopUi == null)
            {
                Debug.LogWarning("[Playtest] Could not create Shop UI.");
                return;
            }

            if (_shopUi.IsOpen)
            {
                _shopUi.CloseShop();
                return;
            }

            ForceOpenShop();
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

            if (_shopUi.IsOpen)
                return;

            if (shop == null)
                shop = _shopUi.ShopDefinition;

            if (FindFirstObjectByType<PlayerWallet>() == null && _inventory != null)
                _inventory.gameObject.AddComponent<PlayerWallet>();

            var handmade = InventoryDisplayUI.Active ?? FindFirstObjectByType<InventoryDisplayUI>();
            if (handmade != null)
                _shopUi.AssignHandmadeInventory(handmade);

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

        public void ToggleAudioSettings() => AudioSettings.ToggleOpen();

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
            if (digit < 0 || digit > 9) return false;

            var kb = Keyboard.current;
            if (kb == null) return false;

            var key = digit == 0 ? Key.Digit0 : (Key)((int)Key.Digit1 + (digit - 1));
            return kb[key].wasPressedThisFrame;
        }

        private static bool WasKeyPressed(KeyCode keyCode)
        {
            var kb = Keyboard.current;
            if (kb == null) return false;

            return keyCode switch
            {
                KeyCode.V => kb.vKey.wasPressedThisFrame,
                KeyCode.Escape => kb.escapeKey.wasPressedThisFrame,
                _ => false
            };
        }
#else
        private static void EnsureKeyboardDevice() { }

        private static bool IsShiftHeld() =>
            Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        private static bool WasBackquotePressed() => Input.GetKeyDown(KeyCode.BackQuote);

        private static bool WasDigitPressed(int digit) =>
            digit >= 0 && digit <= 9 && Input.GetKeyDown(KeyCode.Alpha0 + digit);

        private static bool WasKeyPressed(KeyCode keyCode) => Input.GetKeyDown(keyCode);
#endif
    }
}

