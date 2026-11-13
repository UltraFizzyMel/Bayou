using Bayou.Inventory;
using Bayou.Quests;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Bayou.World
{
    /// <summary>
    /// The foggy-marsh accordion stays hidden until Lost Accordion has started.
    /// Once that quest is underway it can be picked up; it is not respawned if the player already has it.
    /// </summary>
    public sealed class AccordionWorldGate : MonoBehaviour
    {
        private const string ItemId = "Item_Accordion";

        private bool _subscribed;
        private float _nextRefresh;
        private bool _pickupReady;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Ensure();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Ensure();

        private static void Ensure()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid()) return;
            if (string.Equals(scene.name, "MainMenu", System.StringComparison.OrdinalIgnoreCase))
                return;

            if (Object.FindFirstObjectByType<AccordionWorldGate>() != null) return;
            var go = new GameObject(nameof(AccordionWorldGate));
            go.AddComponent<AccordionWorldGate>();
        }

        private void OnEnable() => Refresh();

        private void OnDisable()
        {
            var events = GameEventManager.Instance != null ? GameEventManager.Instance.questEvents : null;
            if (events != null)
                events.onQuestStateChange -= OnQuestState;
            _subscribed = false;
        }

        private void Update()
        {
            TrySubscribe();
            if (Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + 0.5f;
            Refresh();
        }

        private void TrySubscribe()
        {
            if (_subscribed) return;
            var events = GameEventManager.Instance != null ? GameEventManager.Instance.questEvents : null;
            if (events == null) return;
            events.onQuestStateChange += OnQuestState;
            _subscribed = true;
            Refresh();
        }

        private void OnQuestState(Quest quest)
        {
            if (quest?.info == null) return;
            if (!string.Equals(quest.info.id, QuestIds.LostAccordion, System.StringComparison.Ordinal))
                return;
            Refresh();
        }

        private void Refresh()
        {
            var accordion = FindAccordion();
            if (accordion == null) return;

            var show = QuestHasStarted() && !AlreadyCollected();
            if (show)
                EnsurePickup(accordion);

            if (accordion.activeSelf != show)
                accordion.SetActive(show);
        }

        private void EnsurePickup(GameObject accordion)
        {
            if (_pickupReady && accordion.GetComponent<QuestItemPickup>() != null)
                return;

            var col = accordion.GetComponent<Collider>();
            if (col != null)
                col.isTrigger = true;

            var sphere = accordion.GetComponent<SphereCollider>();
            if (sphere == null)
                sphere = accordion.AddComponent<SphereCollider>();
            sphere.isTrigger = true;
            sphere.radius = 1.7f;

            var pickup = accordion.GetComponent<QuestItemPickup>();
            if (pickup == null)
                pickup = accordion.AddComponent<QuestItemPickup>();

            if (_pickupReady) return;

            var def = Resources.Load<ItemDefinition>("Bayou/Items/Item_Accordion");
            if (def == null) return;
            pickup.Bind(def, "Pick up", addStraightToBag: true);
            _pickupReady = true;
        }

        private static bool QuestHasStarted()
        {
            var quests = QuestManager.Resolve();
            if (quests == null || !quests.TryGetQuest(QuestIds.LostAccordion, out var quest) || quest == null)
                return false;

            return quest.state == QuestState.IN_PROGRESS ||
                   quest.state == QuestState.CAN_FINISH ||
                   quest.state == QuestState.FINISHED;
        }

        private static bool AlreadyCollected()
        {
            if (PlayerHasAccordion()) return true;

            // Turning the quest in consumes the accordion, so a finished quest means it was already picked up.
            var quests = QuestManager.Resolve();
            return quests != null &&
                   quests.TryGetQuest(QuestIds.LostAccordion, out var quest) &&
                   quest != null &&
                   quest.state == QuestState.FINISHED;
        }

        private static bool PlayerHasAccordion()
        {
            var inv = InventoryController.Instance;
            return inv != null && inv.HasItemsById(ItemId, 1);
        }

        private static GameObject FindAccordion()
        {
            var transforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < transforms.Length; i++)
            {
                var t = transforms[i];
                if (t == null) continue;
                var n = t.name;
                if (n != "Accordian" && n != "Accordion" && n != "AccordionPickup")
                    continue;
                if (t.parent != null &&
                    (t.parent.name == "Accordian" || t.parent.name == "Accordion"))
                    continue;
                return t.gameObject;
            }

            return null;
        }
    }
}
