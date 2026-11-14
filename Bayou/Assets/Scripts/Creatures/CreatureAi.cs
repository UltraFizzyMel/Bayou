using UnityEngine;

namespace Bayou.Creatures
{
    /// <summary>Shared freeze for snakes, crocs, and spiders.</summary>
    public static class CreatureAi
    {
        public static bool IsFrozen =>
            Time.timeScale <= 0f || Bayou.GameplayPause.IsDialoguePlaying;
    }

    public static class CreatureAggro
    {
        public static event System.Action Began;

        public static void Notify() => Began?.Invoke();
    }
}
