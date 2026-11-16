namespace Bayou.Audio
{
    /// <summary>
    /// Accordion version of the foggy marsh and brackish shore songs.
    /// With-accordion plays only after the accordion is back, it was not sold or sunk,
    /// Lost Accordion is not in progress, and Olivier is alive.
    /// Which place is playing is decided by TransitionMusicDirector.
    /// </summary>
    public static class SwampMusicDirector
    {
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
