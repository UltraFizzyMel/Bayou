using Bayou.Fishing;
using Bayou.Inventory;
using Bayou.Rendering;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Bayou.Quests
{
    /// <summary>
    /// Fresh start: $0, empty hands, hand net on the ground near spawn, first quest auto-starts.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StarterLoadout : MonoBehaviour
    {
        public const string PickupName = "NetPickup";
        private const string NetItemPath = "Bayou/Items/Item_HandNet";
        private const string NetQuestId = "CollectNetQuest";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            // AfterSceneLoad only runs for the first scene. Player builds start on MainMenu.
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Ensure();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Ensure();

        private static void Ensure()
        {
            if (!IsGameplayScene()) return;
            if (Object.FindFirstObjectByType<StarterLoadout>() != null) return;
            var go = new GameObject("StarterLoadout");
            go.AddComponent<StarterLoadout>();
        }

        private void Start() => StartCoroutine(SetupNextFrame());

        private void Update()
        {
            if (Time.frameCount % 30 != 0) return;
            EnsureNetPickup();
        }

        private System.Collections.IEnumerator SetupNextFrame()
        {
            yield return null;
            Bayou.Rendering.WorldItemVisual.PatchScenePickups();
            EnsureNetPickup();
            EnsureNetQuest();
            yield return null;
            Bayou.Rendering.WorldItemVisual.PatchScenePickups();
        }

        private static bool IsGameplayScene()
        {
            var name = SceneManager.GetActiveScene().name;
            if (string.Equals(name, "MainMenu", System.StringComparison.OrdinalIgnoreCase))
                return false;
            if (string.Equals(name, "InventoryTest", System.StringComparison.OrdinalIgnoreCase))
                return false;
            if (string.Equals(name, "TerrainTest", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "MovementTest", System.StringComparison.OrdinalIgnoreCase))
                return true;
            return Object.FindFirstObjectByType<QuestManager>(FindObjectsInactive.Include) != null ||
                   Object.FindFirstObjectByType<BayouFishingEquipment>(FindObjectsInactive.Include) != null;
        }

        private static void EnsureNetPickup()
        {
            var inv = InventoryController.Instance ?? Object.FindFirstObjectByType<InventoryController>();
            var owned = inv != null && inv.HasItemsById("Item_HandNet", 1);
            var pickups = FindNetPickups();

            if (owned)
            {
                for (var i = 0; i < pickups.Length; i++)
                {
                    if (pickups[i] != null)
                        Object.Destroy(pickups[i].gameObject);
                }
                return;
            }

            if (pickups.Length > 1)
            {
                for (var i = 1; i < pickups.Length; i++)
                {
                    if (pickups[i] != null)
                        Object.Destroy(pickups[i].gameObject);
                }
                return;
            }

            if (pickups.Length == 1)
                return;

            var item = Resources.Load<ItemDefinition>(NetItemPath);
            if (item == null)
            {
                Debug.LogWarning("[Starter] Missing Item_HandNet.");
                return;
            }

            var go = new GameObject(PickupName);
            go.transform.position = DefaultNetPosition();

            var col = go.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 1.7f;

            var pickup = go.AddComponent<QuestItemPickup>();
            pickup.Bind(item, "Pick up", addStraightToBag: false);

            var marker = go.AddComponent<QuestMarkerTarget>();
            marker.Bind(NetQuestId, item.Id, "Hand net");

            WorldItemVisual.BuildNet(go.transform, replaceExisting: true);
            WorldItemVisual.SnapToGround(go.transform, 0.22f);
        }

        private static QuestItemPickup[] FindNetPickups()
        {
            var all = Object.FindObjectsByType<QuestItemPickup>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var count = 0;
            for (var i = 0; i < all.Length; i++)
            {
                if (IsHandNetPickup(all[i]))
                    count++;
            }

            if (count == 0)
                return System.Array.Empty<QuestItemPickup>();

            var result = new QuestItemPickup[count];
            var n = 0;
            for (var i = 0; i < all.Length; i++)
            {
                if (!IsHandNetPickup(all[i])) continue;
                result[n++] = all[i];
            }

            return result;
        }

        private static bool IsHandNetPickup(QuestItemPickup pickup)
        {
            if (pickup == null) return false;
            if (pickup.gameObject.name == PickupName)
                return true;
            return pickup.Item != null && WorldItemVisual.IsNet(pickup.Item.Id);
        }

        private static Vector3 DefaultNetPosition()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                var pos = player.transform.position
                          + player.transform.forward * 3.4f
                          + player.transform.right * 1.1f;
                pos.y = player.transform.position.y;
                return pos;
            }

            var scene = SceneManager.GetActiveScene().name;
            if (string.Equals(scene, "TerrainTest", System.StringComparison.OrdinalIgnoreCase))
                return new Vector3(-2.5f, 1.75f, -125.2f);

            return new Vector3(-8.8f, 1.75f, -89.2f);
        }

        private static void EnsureNetQuest()
        {
            var manager = QuestManager.Resolve();
            if (manager == null) return;

            var inv = InventoryController.Instance ?? Object.FindFirstObjectByType<InventoryController>();
            var hasNet = inv != null && inv.HasPlacedItemsById("Item_HandNet", 1);
            if (hasNet) return;

            if (!manager.TryGetQuest(NetQuestId, out var quest) || quest == null)
                return;

            if (quest.state == QuestState.FINISHED)
                manager.ForceRestart(NetQuestId);
            else if (quest.state != QuestState.IN_PROGRESS && quest.state != QuestState.CAN_FINISH)
                manager.StartQuest(NetQuestId);
        }
    }
}
