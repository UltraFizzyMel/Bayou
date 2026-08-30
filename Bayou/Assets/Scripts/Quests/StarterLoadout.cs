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
            var hoop = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            hoop.name = "NetHoop";
            hoop.transform.SetParent(parent, false);
            hoop.transform.localPosition = new Vector3(0f, 0.18f, 0f);
            hoop.transform.localScale = new Vector3(0.55f, 0.035f, 0.55f);
            Object.Destroy(hoop.GetComponent<Collider>());

            var handle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            handle.name = "NetHandle";
            handle.transform.SetParent(parent, false);
            handle.transform.localPosition = new Vector3(0f, 0.12f, -0.42f);
            handle.transform.localScale = new Vector3(0.07f, 0.07f, 0.55f);
            Object.Destroy(handle.GetComponent<Collider>());

            var brown = new Color(0.35f, 0.24f, 0.14f, 1f);
            ApplyColor(hoop, new Color(0.22f, 0.38f, 0.28f, 1f));
            ApplyColor(handle, brown);
        }

        private static void ApplyColor(GameObject go, Color color)
        {
            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer == null) return;
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null) return;
            var mat = new Material(shader);
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color"))
                mat.color = color;
            renderer.sharedMaterial = mat;
        }
    }
}
