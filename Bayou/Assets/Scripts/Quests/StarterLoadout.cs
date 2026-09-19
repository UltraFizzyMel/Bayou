using Bayou.Fishing;
using Bayou.Inventory;
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
            if (!IsGameplayScene()) return;
            if (Object.FindFirstObjectByType<StarterLoadout>() != null) return;
            var go = new GameObject("StarterLoadout");
            go.AddComponent<StarterLoadout>();
        }

        private void Start() => StartCoroutine(SetupNextFrame());

        private System.Collections.IEnumerator SetupNextFrame()
        {
            yield return null;
            EnsureNetPickup();
            EnsureNetQuest();
        }

        private static bool IsGameplayScene()
        {
            var name = SceneManager.GetActiveScene().name;
            if (string.Equals(name, "MainMenu", System.StringComparison.OrdinalIgnoreCase))
                return false;
            if (string.Equals(name, "InventoryTest", System.StringComparison.OrdinalIgnoreCase))
                return false;
            return Object.FindFirstObjectByType<QuestManager>(FindObjectsInactive.Include) != null ||
                   Object.FindFirstObjectByType<BayouFishingEquipment>(FindObjectsInactive.Include) != null;
        }

        private static void EnsureNetPickup()
        {
            var inv = InventoryController.Instance ?? Object.FindFirstObjectByType<InventoryController>();
            if (inv != null && inv.HasItemsById("Item_HandNet", 1))
                return;
            if (GameObject.Find(PickupName) != null)
                return;

            var item = Resources.Load<ItemDefinition>(NetItemPath);
            if (item == null)
            {
                Debug.LogWarning("[Starter] Missing Item_HandNet.");
                return;
            }

            var player = GameObject.FindGameObjectWithTag("Player");
            var pos = player != null
                ? player.transform.position + player.transform.forward * 3.6f + Vector3.right * 1.1f
                : new Vector3(-8.2f, 1.15f, -88.4f);
            pos.y = player != null ? player.transform.position.y : pos.y;

            var go = new GameObject(PickupName);
            go.transform.position = pos;

            var col = go.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 1.7f;

            var pickup = go.AddComponent<QuestItemPickup>();
            pickup.Bind(item, "Pick up");

            var marker = go.AddComponent<QuestMarkerTarget>();
            marker.Bind(NetQuestId, item.Id, "Hand net");

            CreateNetVisual(go.transform);
        }

        private static void EnsureNetQuest()
        {
            var manager = QuestManager.Resolve();
            if (manager == null) return;

            var inv = InventoryController.Instance ?? Object.FindFirstObjectByType<InventoryController>();
            var hasNet = inv != null && inv.HasItemsById("Item_HandNet", 1);
            if (hasNet) return;

            if (!manager.TryGetQuest(NetQuestId, out var quest) || quest == null)
                return;

            if (quest.state == QuestState.FINISHED)
                manager.ForceRestart(NetQuestId);
            else if (quest.state != QuestState.IN_PROGRESS && quest.state != QuestState.CAN_FINISH)
                manager.StartQuest(NetQuestId);
        }

        private static void CreateNetVisual(Transform parent)
        {
            Bayou.Rendering.WorldItemVisual.BuildNet(parent, replaceExisting: true);
        }
    }
}
