using System;
using System.Collections;
using System.IO;
using Bayou.Inventory;
using Bayou.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Bayou.Save
{
    [DisallowMultipleComponent]
    public sealed class GameSaveSystem : MonoBehaviour
    {
        public static GameSaveSystem Instance { get; private set; }

        private const string PlayerSaveFileName = "bayou_save.json";
        private const string EditorSaveFileName = "bayou_save_editor.json";

        [SerializeField] private ItemCatalog itemCatalog;
        [SerializeField] private bool loadSaveOnStart = true;
        [SerializeField] private string playerTag = "Player";

        public bool HasSaveFile => File.Exists(SaveFilePath);
        public string LastBonfireId { get; private set; }
        public ItemCatalog ItemCatalog => itemCatalog;
        public bool HasSessionCheckpoint { get; private set; }

        public static string SaveFilePath => Path.Combine(Application.persistentDataPath, SaveFileName);

        private static string SaveFileName => Application.isEditor ? EditorSaveFileName : PlayerSaveFileName;

        /// <summary>When true, the next Start skips loading the save.</summary>
        public static bool SuppressNextLoad { get; set; }

        public event Action GameSaved;
        public event Action GameLoaded;

        private bool _capturedSpawn;
        private Vector3 _spawnPosition;
        private Quaternion _spawnRotation;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
            SuppressNextLoad = false;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            itemCatalog?.BuildLookup();
            HasSessionCheckpoint = false;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private IEnumerator Start()
        {
            yield return null;
            CaptureSpawnIfNeeded();

            var suppress = SuppressNextLoad;
            SuppressNextLoad = false;

            // Editor Play Mode must not consume the real player save — that teleports
            // testers to an old campfire and desyncs inventory from a fresh quest log.
            if (Application.isEditor)
            {
                Debug.Log("[Save] Editor play starts fresh. Rest at a campfire to set a death checkpoint for this session.");
                yield break;
            }

            if (loadSaveOnStart && !suppress && HasSaveFile)
                Load();
        }

        public bool Save(string bonfireId)
        {
            var player = FindPlayer();
            var inventory = InventoryController.Instance;
            var wallet = PlayerWallet.Instance;

            if (player == null || inventory?.Bag == null)
            {
                Debug.LogWarning("[Save] Missing player or inventory.");
                return false;
            }

            var health = player.GetComponent<PlayerHealth>() ?? PlayerHealth.Resolve();
            var data = new GameSaveData
            {
                sceneName = SceneManager.GetActiveScene().name,
                lastBonfireId = bonfireId,
                playerX = player.position.x,
                playerY = player.position.y,
                playerZ = player.position.z,
                playerRotY = player.eulerAngles.y,
                walletBalance = wallet != null ? wallet.Balance : 0,
                playerHealth = health != null ? health.Current : PlayerHealth.DefaultMaxHealth,
                playerMaxHealth = health != null ? health.Max : PlayerHealth.DefaultMaxHealth,
                inventoryItems = CaptureInventory(inventory)
            };

            try
            {
                var json = JsonUtility.ToJson(data, prettyPrint: true);
                File.WriteAllText(SaveFilePath, json);
                LastBonfireId = bonfireId;
                HasSessionCheckpoint = true;
                GameSaved?.Invoke();
                Debug.Log($"[Save] Game saved at bonfire '{bonfireId}'.");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Save] Failed to write save file: {ex.Message}");
                return false;
            }
        }

        public bool Load()
        {
            if (!HasSaveFile)
                return false;

            try
            {
                var json = File.ReadAllText(SaveFilePath);
                var data = JsonUtility.FromJson<GameSaveData>(json);
                if (data == null)
                    return false;

                return ApplySaveData(data);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Save] Failed to load save file: {ex.Message}");
                return false;
            }
        }

        private bool ApplySaveData(GameSaveData data)
        {
            if (data.sceneName != SceneManager.GetActiveScene().name)
            {
                Debug.LogWarning(
                    $"[Save] Save scene '{data.sceneName}' differs from active scene. Loading inventory/wallet only.");
            }

            LastBonfireId = data.lastBonfireId;
            HasSessionCheckpoint = true;

            var wallet = PlayerWallet.Instance;
            if (wallet != null)
                wallet.SetBalance(data.walletBalance);

            var inventory = InventoryController.Instance;
            if (inventory != null)
                RestoreInventory(inventory, data.inventoryItems);

            var player = FindPlayer();
            if (player != null)
            {
                PlacePlayer(
                    player,
                    new Vector3(data.playerX, data.playerY, data.playerZ),
                    Quaternion.Euler(0f, data.playerRotY, 0f));
                var health = player.GetComponent<PlayerHealth>() ?? PlayerHealth.EnsureOn(player.gameObject);
                if (data.playerMaxHealth > 0)
                    health.Restore(data.playerHealth, data.playerMaxHealth);
            }

            SnapCamera();
            GameLoaded?.Invoke();
            Debug.Log($"[Save] Game loaded from bonfire '{data.lastBonfireId}'.");
            return true;
        }

        /// <summary>
        /// Death respawn. Uses a campfire rest from this session; otherwise the scene spawn.
        /// Editor Play Mode never falls back to a previous run's save file.
        /// </summary>
        public bool RespawnAfterDeath()
        {
            CaptureSpawnIfNeeded();

            if (HasSessionCheckpoint && HasSaveFile && Load())
                return true;

            RestoreSpawn();
            return false;
        }

        private void CaptureSpawnIfNeeded()
        {
            if (_capturedSpawn) return;
            var player = FindPlayer();
            if (player == null) return;

            _spawnPosition = player.position;
            _spawnRotation = player.rotation;
            _capturedSpawn = true;
        }

        private void RestoreSpawn()
        {
            var player = FindPlayer();
            if (player != null && _capturedSpawn)
                PlacePlayer(player, _spawnPosition, _spawnRotation);

            var health = player != null
                ? player.GetComponent<PlayerHealth>() ?? PlayerHealth.EnsureOn(player.gameObject)
                : PlayerHealth.Resolve();
            health?.HealToFull();
            SnapCamera();
        }

        private static void PlacePlayer(Transform player, Vector3 position, Quaternion rotation)
        {
            var motor = player.GetComponent<BayouCharacterMotor>();
            if (motor != null)
            {
                motor.Teleport(position, rotation);
                return;
            }

            player.SetPositionAndRotation(position, rotation);
            var rb = player.GetComponent<Rigidbody>();
            if (rb == null) return;
            rb.position = position;
            rb.rotation = rotation;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        private static void SnapCamera()
        {
            var cam = FindFirstObjectByType<Bayou.CameraControl.BayouFollowCamera>();
            cam?.SnapToTarget();
        }

        private SavedItemEntry[] CaptureInventory(InventoryController inventory)
        {
            var entries = new System.Collections.Generic.List<SavedItemEntry>();
            foreach (var item in inventory.Bag.AllItems)
            {
                if (item?.definition == null || !item.IsPlaced) continue;

                entries.Add(new SavedItemEntry
                {
                    itemId = item.definition.name,
                    instanceId = item.instanceId,
                    compartmentId = item.compartmentId,
                    gridX = item.gridX,
                    gridY = item.gridY,
                    rotation = item.rotation,
                    stackCount = item.stackCount
                });
            }

            return entries.ToArray();
        }

        private void RestoreInventory(InventoryController inventory, SavedItemEntry[] entries)
        {
            if (itemCatalog == null)
            {
                Debug.LogWarning("[Save] Item catalog missing — cannot restore inventory.");
                return;
            }

            inventory.ClearAllItems();
            if (entries == null) return;

            foreach (var entry in entries)
            {
                var def = itemCatalog.Resolve(entry.itemId);
                if (def == null)
                {
                    Debug.LogWarning($"[Save] Unknown item id '{entry.itemId}'.");
                    continue;
                }

                var instance = new InventoryItemInstance(def, entry.rotation)
                {
                    instanceId = string.IsNullOrWhiteSpace(entry.instanceId)
                        ? Guid.NewGuid().ToString("N")
                        : entry.instanceId,
                    stackCount = entry.stackCount
                };

                if (!inventory.TryPlace(instance, entry.compartmentId, entry.gridX, entry.gridY, entry.rotation))
                    inventory.TryAddItem(def, entry.rotation);
            }
        }

        private Transform FindPlayer()
        {
            var go = GameObject.FindGameObjectWithTag(playerTag);
            return go != null ? go.transform : null;
        }

        public bool DeleteSave()
        {
            if (!HasSaveFile)
                return false;

            try
            {
                File.Delete(SaveFilePath);
                LastBonfireId = null;
                HasSessionCheckpoint = false;
                Debug.Log("[Save] Save file deleted.");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Save] Failed to delete save file: {ex.Message}");
                return false;
            }
        }
    }
}
