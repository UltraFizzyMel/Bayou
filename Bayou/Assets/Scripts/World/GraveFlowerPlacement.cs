using Bayou.Inventory;
using Bayou.Quests;
using Bayou.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Bayou.World
{
    /// <summary>
    /// The marsh flower is laid on Caliste's grave. The blossom stays hidden until placement.
    /// Magnolias (the cluster) is a different object and is not touched here.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public sealed class GraveFlowerPlacement : MonoBehaviour, IInteractionPromptSource
    {
        public const string GraveObjectName = VisitGravesQuestBootstrap.CalisteGraveName;
        public const string VisualName = "PlacedFlower";
        private const string FlowerId = "Item_HerbalistFlower";

        private GameObject _visual;
        private bool _subscribed;
        private float _nextSeek;
        private int _claimFrame = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Ensure();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Ensure();

        public static void Ensure()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid()) return;
            if (string.Equals(scene.name, "MainMenu", System.StringComparison.OrdinalIgnoreCase))
                return;

            var grave = GameObject.Find(GraveObjectName) ?? VisitGravesQuestBootstrap.FindCalisteGraveFallback();
            if (grave == null) return;
            if (grave.GetComponent<GraveFlowerPlacement>() == null)
                grave.AddComponent<GraveFlowerPlacement>();
        }

        /// <summary>True when E on this grave should place the flower instead of lighting it.</summary>
        public bool ClaimsInteract => (CanPlace() && InRange()) || _claimFrame == Time.frameCount;

        private void OnEnable()
        {
            InteractionPromptBroker.Register(this);
            RefreshVisual();
        }

        private void OnDisable()
        {
            InteractionPromptBroker.Unregister(this);
            var events = GameEventManager.Instance != null ? GameEventManager.Instance.questEvents : null;
            if (events != null)
                events.onQuestStateChange -= OnQuest;
            _subscribed = false;
        }

        private void Update()
        {
            TrySubscribe();
            if (_visual == null && Time.unscaledTime >= _nextSeek)
            {
                _nextSeek = Time.unscaledTime + 0.5f;
                RefreshVisual();
            }

            if (!CanPlace() || !InRange()) return;
            if (Bayou.GameplayPause.BlocksWorldInteract || Bayou.GameplayPause.IsDialoguePlaying)
                return;

            var input = InputManager.GetInstance();
            if (input == null || !input.GetInteractPressed())
                return;

            Place();
        }

        public bool TryGetInteractionPrompt(out InteractionPrompt prompt)
        {
            prompt = default;
            if (!CanPlace() || !InRange()) return false;
            if (Bayou.GameplayPause.BlocksWorldInteract || Bayou.GameplayPause.IsDialoguePlaying)
                return false;

            prompt = new InteractionPrompt("E", "Place the flower", 64, DistanceSq());
            return true;
        }

        private void TrySubscribe()
        {
            if (_subscribed) return;
            var events = GameEventManager.Instance != null ? GameEventManager.Instance.questEvents : null;
            if (events == null) return;
            events.onQuestStateChange += OnQuest;
            _subscribed = true;
        }

        private void OnQuest(Quest quest)
        {
            if (quest?.info == null) return;
            if (!string.Equals(QuestIdMap.Canonical(quest.info.id), QuestIds.VisitGraves, System.StringComparison.Ordinal))
                return;
            RefreshVisual();
        }

        private bool CanPlace()
        {
            if (IsPlaced()) return false;
            if (!QuestReady()) return false;
            var inv = InventoryController.Instance;
            return inv != null && inv.HasItemsById(FlowerId, 1);
        }

        private void Place()
        {
            var quests = QuestManager.Resolve();
            if (quests == null) return;

            // FinishQuest consumes the flower once via QuestRewards and sets flowerPlaced.
            _claimFrame = Time.frameCount;
            quests.FinishQuest(QuestIds.VisitGraves);
            RefreshVisual();
        }

        private static bool QuestReady()
        {
            var quests = QuestManager.Resolve();
            if (quests == null || !quests.TryGetQuest(QuestIds.VisitGraves, out var quest) || quest == null)
                return false;
            return quest.state == QuestState.IN_PROGRESS || quest.state == QuestState.CAN_FINISH;
        }

        private static bool IsPlaced()
        {
            var dialogue = DialogueManager.GetInstance();
            if (dialogue != null &&
                dialogue.TryGetBoolVariable("flowerPlaced", out var placed) &&
                placed)
                return true;

            var quests = QuestManager.Resolve();
            return quests != null &&
                   quests.TryGetQuest(QuestIds.VisitGraves, out var quest) &&
                   quest != null &&
                   quest.state == QuestState.FINISHED;
        }

        private void RefreshVisual()
        {
            EnsureVisual();
            if (_visual == null) return;
            var show = IsPlaced();
            if (_visual.activeSelf != show)
                _visual.SetActive(show);
        }

        private void EnsureVisual()
        {
            if (_visual != null) return;

            var existing = transform.Find(VisualName);
            if (existing == null)
            {
                var go = new GameObject(VisualName);
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(0.35f, 1.15f, 0.25f);
                existing = go.transform;
                CopyBlossomMesh(go);
            }

            var rosary = FindRosary();
            if (rosary != null && existing.childCount > 0)
                existing.position = rosary.position + Vector3.up * 0.12f;

            _visual = existing.gameObject;
            if (!IsPlaced())
                _visual.SetActive(false);
        }

        private static Transform FindRosary()
        {
            var transforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < transforms.Length; i++)
            {
                var t = transforms[i];
                if (t != null && t.name.StartsWith("Rosary", System.StringComparison.OrdinalIgnoreCase))
                    return t;
            }

            return null;
        }

        private static void CopyBlossomMesh(GameObject host)
        {
            var transforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Renderer source = null;
            for (var i = 0; i < transforms.Length; i++)
            {
                var t = transforms[i];
                if (t == null) continue;
                if (!UnderMagnolias(t)) continue;
                if (t.name.IndexOf("Flower", System.StringComparison.OrdinalIgnoreCase) < 0 &&
                    t.name.IndexOf("Petal", System.StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                source = t.GetComponent<Renderer>();
                if (source == null)
                    source = t.GetComponentInChildren<Renderer>(true);
                if (source != null)
                    break;
            }

            if (source == null) return;

            var copy = Object.Instantiate(source.gameObject, host.transform);
            copy.name = "Blossom";
            copy.SetActive(true);
            foreach (var col in copy.GetComponentsInChildren<Collider>(true))
            {
                if (col != null)
                    col.enabled = false;
            }

            var rend = copy.GetComponentInChildren<Renderer>(true);
            if (rend == null) return;
            var size = rend.bounds.size;
            var max = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
            if (max > 0.05f)
                copy.transform.localScale *= 0.42f / max;
        }

        private static bool UnderMagnolias(Transform t)
        {
            for (var p = t; p != null; p = p.parent)
            {
                if (p.name == "Magnolias")
                    return true;
            }

            return false;
        }

        private bool InRange()
        {
            var player = Bayou.Player.PlayerLocator.Transform;
            if (player == null) return false;
            var flat = player.position - transform.position;
            flat.y = 0f;
            if (flat.sqrMagnitude <= 6.5f * 6.5f)
                return true;

            var cols = GetComponentsInChildren<Collider>();
            for (var i = 0; i < cols.Length; i++)
            {
                if (cols[i] == null) continue;
                var closest = cols[i].ClosestPoint(player.position);
                if ((closest - player.position).sqrMagnitude <= 2.6f * 2.6f)
                    return true;
            }

            return false;
        }

        private float DistanceSq()
        {
            var player = Bayou.Player.PlayerLocator.Transform;
            if (player == null) return 0f;
            var d = player.position - transform.position;
            d.y = 0f;
            return d.sqrMagnitude;
        }
    }
}
