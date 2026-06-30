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

        private const string SaveFileName = "bayou_save.json";

        [SerializeField] private ItemCatalog itemCatalog;
        [SerializeField] private bool loadSaveOnStart = true;
        [SerializeField] private string playerTag = "Player";

        public bool HasSaveFile => File.Exists(SaveFilePath);
        public string LastBonfireId { get; private set; }
        public ItemCatalog ItemCatalog => itemCatalog;

        public static string SaveFilePath => Path.Combine(Application.persistentDataPath, SaveFileName);

        public event Action GameSaved;
        public event Action GameLoaded;

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
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private IEnumerator Start()
        {
            yield return null;
            if (loadSaveOnStart && HasSaveFile)
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

            var data = new GameSaveData
            {
                sceneName = SceneManager.GetActiveScene().name,
                lastBonfireId = bonfireId,
                playerX = player.position.x,
                playerY = player.position.y,
                playerZ = player.position.z,
                playerRotY = player.eulerAngles.y,
                walletBalance = wallet != null ? wallet.Balance : 0,
                inventoryItems = CaptureInventory(inventory)
            };

            try
            {
                var json = JsonUtility.ToJson(data, prettyPrint: true);
                File.WriteAllText(SaveFilePath, json);
                LastBonfireId = bonfireId;
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

            var wallet = PlayerWallet.Instance;
            if (wallet != null)
                wallet.SetBalance(data.walletBalance);

            var inventory = InventoryController.Instance;
            if (inventory != null)
                RestoreInventory(inventory, data.inventoryItems);

            var player = FindPlayer();
            if (player != null)
            {
                player.position = new Vector3(data.playerX, data.playerY, data.playerZ);
                player.rotation = Quaternion.Euler(0f, data.playerRotY, 0f);
            }

            GameLoaded?.Invoke();
            Debug.Log($"[Save] Game loaded from bonfire '{data.lastBonfireId}'.");
            return true;
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
