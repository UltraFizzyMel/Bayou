using Bayou.World;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Bayou.Audio
{
    /// <summary>
    /// Plays foggy marsh and brackish shore music. The accordion version is used only
    /// after the accordion has been returned, it was not sold or sunk, Lost Accordion
    /// is not in progress, and Olivier is alive. Clips stream through AreaMusicPlayback.
    /// </summary>
    public sealed class SwampMusicDirector : MonoBehaviour
    {
        private AreaMusicSet _set;
        private float _next;
        private AudioClip _current;

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
            if (Object.FindFirstObjectByType<SwampMusicDirector>() != null) return;
            var go = new GameObject(nameof(SwampMusicDirector));
            go.AddComponent<SwampMusicDirector>();
        }

        private void Awake()
        {
            _set = Resources.Load<AreaMusicSet>("Bayou/AreaMusicSet");
        }

        private void Update()
        {
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 0.45f;
            Tick();
        }

        private void Tick()
        {
            if (_set == null)
                _set = Resources.Load<AreaMusicSet>("Bayou/AreaMusicSet");
            if (_set == null) return;

            var player = Bayou.Player.PlayerLocator.Transform;
            if (player == null) return;

            PlayerRegionFlags.Classify(player.position, out var foggy, out var firstShore);
            var second = !foggy && !firstShore && PlayerRegionFlags.IsBrackishPosition(player.position);
            if (!foggy && !firstShore && !second)
                return;

            var withAccordion = WantsAccordion();
            AudioClip clip;
            if (foggy)
                clip = withAccordion ? _set.foggyWith : _set.foggyWithout;
            else
                clip = withAccordion ? _set.brackishWith : _set.brackishWithout;

            if (clip == null)
                return;
            if (clip == _current && AreaMusicPlayback.IsCurrent(clip))
                return;

            _current = clip;
            AreaMusicPlayback.Play(clip, null, 0.62f, 0f, null);
        }

        /// <summary>
        /// With-accordion tracks play only when the accordion is back, not disposed,
        /// Lost Accordion is not underway, and Olivier is alive.
        /// </summary>
        public static bool WantsAccordion()
        {
            if (LostAccordionInProgress())
                return false;
            if (Disposed())
                return false;
            if (!Returned())
                return false;
            return OlivierAlive();
        }

        private static bool LostAccordionInProgress()
        {
            var quests = QuestManager.Resolve();
            return quests != null &&
                   quests.TryGetQuest(QuestIds.LostAccordion, out var quest) &&
                   quest != null &&
                   quest.state == QuestState.IN_PROGRESS;
        }

        private static bool Disposed()
        {
            if (WorldFlags.Has(WorldFlags.DisposedAccordion))
                return true;
            return Ink("accordian_disposed") || Ink("accordion_disposed");
        }

        private static bool Returned()
        {
            if (WorldFlags.Has(WorldFlags.ReturnedAccordion))
                return true;
            return Ink("accordian_returned") || Ink("accordion_returned");
        }

        private static bool OlivierAlive()
        {
            var dialogue = DialogueManager.GetInstance();
            if (dialogue == null)
                return true;

            var alive = true;
            if (dialogue.TryGetBoolVariable("is_olivier_alive", out var a))
                alive = a;
            if (dialogue.TryGetBoolVariable("is_Olivier_alive", out var b))
                alive = alive && b;
            return alive;
        }

        private static bool Ink(string name)
        {
            var dialogue = DialogueManager.GetInstance();
            return dialogue != null &&
                   dialogue.TryGetBoolVariable(name, out var value) &&
                   value;
        }
    }
}
