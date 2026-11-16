using Bayou.Environment;
using Bayou.Inventory;
using Bayou.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Bayou.World
{
    /// <summary>Wires sunken grove sigils, wedding ring, and final trigger after spirit-lanther play.</summary>
    public static class SunkenGroveEndingBootstrap
    {
        private const string RootName = "Sunken Grove Sigils";
        private const string RingTriggerName = "WeddingRingTrigger";
        private const string EndTriggerName = "SunkenGroveEndingTrigger";
        private const string WallColliderName = "Sunken Grove Sigil box collider";
        private const string RingFlag = WorldFlags.WeddingRingFound;
        private const string RingItemId = "Item_WeddingRing";

        private const string RingText =
            "A golden wedding band, it feels slightly warm. It makes you smile but you don't know why.";

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
            if (!scene.IsValid() || scene.name == "MainMenu") return;

            var root = GameObject.Find(RootName);
            if (root == null) return;

            WireSigils(root.transform);
            var ring = EnsureTrigger(root.transform, RingTriggerName, new Vector3(4.2f, -1.2f, 2.1f), new Vector3(3.5f, 2.5f, 3.5f));
            var ending = EnsureTrigger(root.transform, EndTriggerName, new Vector3(7.5f, -1.2f, 2.8f), new Vector3(4f, 3f, 4f));

            if (ring.GetComponent<SunkenGroveRingPickup>() == null)
                ring.AddComponent<SunkenGroveRingPickup>();
            if (ending.GetComponent<SunkenGroveEndingTrigger>() == null)
                ending.AddComponent<SunkenGroveEndingTrigger>();

            var host = root.GetComponent<SunkenGroveEndingHost>();
            if (host == null)
                host = root.AddComponent<SunkenGroveEndingHost>();
            host.Install(root.transform, ring.transform, ending.transform);
        }

        internal static GameObject FindWallCollider(Transform sigilRoot)
        {
            if (sigilRoot == null) return null;
            for (var i = 0; i < sigilRoot.childCount; i++)
            {
                var child = sigilRoot.GetChild(i);
                if (child != null && child.name.IndexOf(WallColliderName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return child.gameObject;
            }

            return GameObject.Find(WallColliderName);
        }

        private static void WireSigils(Transform root)
        {
            for (var i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (child == null) continue;
                var name = child.name;
                if (name.IndexOf("Sigil", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (name.IndexOf("box", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;
                if (child.GetComponent<SpiritSigilDispel>() == null)
                    child.gameObject.AddComponent<SpiritSigilDispel>();
            }
        }

        private static GameObject EnsureTrigger(Transform parent, string objectName, Vector3 localPos, Vector3 size)
        {
            var existing = parent.Find(objectName);
            if (existing != null)
                return existing.gameObject;

            var go = new GameObject(objectName);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = size;
            box.center = Vector3.up * (size.y * 0.35f);
            return go;
        }

        private sealed class SunkenGroveRingPickup : MonoBehaviour
        {
            private void OnTriggerEnter(Collider other)
            {
                if (WorldFlags.Has(RingFlag)) return;
                if (!IsPlayer(other)) return;

                WorldFlags.Set(RingFlag, true);
                GrantRingItem();
                SignReadUi.Show(this, "", RingText);

                var host = transform.parent != null
                    ? transform.parent.GetComponent<SunkenGroveEndingHost>()
                    : null;
                host?.RefreshGates();
            }

            private static bool IsPlayer(Collider other)
            {
                return other.GetComponentInParent<BayouCharacterMotor>() != null;
            }

            private static void GrantRingItem()
            {
                var inv = InventoryController.Instance;
                if (inv == null || inv.CountExact(RingItemId) >= 1) return;
                var catalog = Resources.Load<ItemCatalog>("Bayou/ItemCatalog");
                var def = catalog != null ? catalog.Resolve(RingItemId) : null;
                def ??= Resources.Load<ItemDefinition>($"Bayou/Items/{RingItemId}");
                if (def != null)
                    inv.GrantReward(def, 1);
            }
        }

        private sealed class SunkenGroveEndingTrigger : MonoBehaviour
        {
            private bool _fired;

            private void OnTriggerEnter(Collider other)
            {
                if (_fired || !WorldFlags.Has(RingFlag)) return;
                if (!other.GetComponentInParent<BayouCharacterMotor>()) return;
                _fired = true;

                SignReadUi.Show(this, "The End",
                    "The bayou breathes easier. Whatever waited in the dark has let you go.\n\nThank you for playing.");
                Invoke(nameof(LoadMenu), 6.5f);
            }

            private void LoadMenu()
            {
                Time.timeScale = 1f;
                Bayou.UI.QuestJournalHud.Instance?.Close();
                Bayou.GameplayPause.SyncFromUiState();
                if (Application.CanStreamedLevelBeLoaded("MainMenu"))
                    SceneManager.LoadScene("MainMenu");
                else
                    SceneManager.LoadScene(0);
            }
        }
    }
}