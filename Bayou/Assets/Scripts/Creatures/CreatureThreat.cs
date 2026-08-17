using UnityEngine;

namespace Bayou.Creatures
{
    /// <summary>Shared queries for “is the player being hunted?” used by net combat mode.</summary>
    public static class CreatureThreat
    {
        /// <summary>
        /// True if any living creature is in Active chase and within <paramref name="maxDistance"/> of the player.
        /// </summary>
        public static bool IsPlayerPursued(Transform player, float maxDistance = 45f)
        {
            if (player == null) return false;

            var maxSq = maxDistance * maxDistance;
            var creatures = Object.FindObjectsByType<CreatureController>(FindObjectsSortMode.None);
            for (var i = 0; i < creatures.Length; i++)
            {
                var c = creatures[i];
                if (c == null || !c.isActiveAndEnabled || c.IsCaught) continue;
                if (!c.IsActive) continue;

                var d = c.transform.position - player.position;
                d.y = 0f;
                if (d.sqrMagnitude <= maxSq)
                    return true;
            }

            return false;
        }

        public static int CountActiveHunters(Transform player, float maxDistance = 45f)
        {
            if (player == null) return 0;
            var maxSq = maxDistance * maxDistance;
            var n = 0;
            var creatures = Object.FindObjectsByType<CreatureController>(FindObjectsSortMode.None);
            for (var i = 0; i < creatures.Length; i++)
            {
                var c = creatures[i];
                if (c == null || !c.isActiveAndEnabled || c.IsCaught || !c.IsActive) continue;
                var d = c.transform.position - player.position;
                d.y = 0f;
                if (d.sqrMagnitude <= maxSq)
                    n++;
            }

            return n;
        }
    }
}
