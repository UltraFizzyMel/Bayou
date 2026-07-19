using System.Collections.Generic;

namespace Bayou.Combat
{
    /// <summary>
    /// Tracks hostiles currently engaged with the player. Used by the fishing rod to switch fish ↔ fight.
    /// </summary>
    public static class CombatPresence
    {
        private static readonly HashSet<object> Engaged = new();

        public static bool IsPlayerThreatened => Engaged.Count > 0;

        public static void SetEngaged(object key, bool engaged)
        {
            if (key == null) return;
            if (engaged) Engaged.Add(key);
            else Engaged.Remove(key);
        }

        public static void Clear() => Engaged.Clear();
    }
}
