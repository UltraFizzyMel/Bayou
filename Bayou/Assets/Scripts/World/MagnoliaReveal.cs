using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Bayou.World
{
    /// <summary>
    /// Magnolia clusters stay hidden until Sabine's grave revelation (VisitGravesQuest) is finished.
    /// The Magnolias parent is left in place; only its children are toggled.
    /// </summary>
    public sealed class MagnoliaReveal : MonoBehaviour
    {
        private bool _subscribed;
        private float _nextRefresh;
        private int _misses;
        private Transform[] _beds = System.Array.Empty<Transform>();

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
            if (Object.FindFirstObjectByType<MagnoliaReveal>() != null) return;
            var go = new GameObject(nameof(MagnoliaReveal));
            go.AddComponent<MagnoliaReveal>();
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
            if (_misses >= 8 && _beds.Length == 0) return;
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
            if (!string.Equals(QuestIdMap.Canonical(quest.info.id), QuestIds.VisitGraves, System.StringComparison.Ordinal))
                return;
            Refresh();
        }

        private void Refresh()
        {
            if (_beds.Length == 0)
            {
                _beds = FindBeds();
                if (_beds.Length == 0)
                {
                    _misses++;
                    return;
                }
            }

            var show = QuestFinished();
            for (var i = 0; i < _beds.Length; i++)
            {
                var bed = _beds[i];
                if (bed == null) continue;
                for (var c = 0; c < bed.childCount; c++)
                {
                    var child = bed.GetChild(c);
                    if (child == null) continue;
                    if (child.gameObject.activeSelf != show)
                        child.gameObject.SetActive(show);
                }
            }
        }

        private static bool QuestFinished()
        {
            var quests = QuestManager.Resolve();
            if (quests == null || !quests.TryGetQuest(QuestIds.VisitGraves, out var quest) || quest == null)
                return false;
            return quest.state == QuestState.FINISHED;
        }

        private static Transform[] FindBeds()
        {
            var found = new List<Transform>();
            var transforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < transforms.Length; i++)
            {
                var t = transforms[i];
                if (t == null || t.name != "Magnolias") continue;
                found.Add(t);
            }

            return found.ToArray();
        }
    }
}
