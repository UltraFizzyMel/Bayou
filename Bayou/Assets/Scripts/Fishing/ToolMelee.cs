using Bayou.Creatures;
using UnityEngine;

namespace Bayou.Fishing
{
    /// <summary>Shared overlap swing for net / rod melee.</summary>
    internal static class ToolMelee
    {
        private static readonly Collider[] Buffer = new Collider[32];

        public static bool TryHitCreatures(Vector3 center, float radius, NetHitSource source)
        {
            var count = Physics.OverlapSphereNonAlloc(
                center,
                radius,
                Buffer,
                ~0,
                QueryTriggerInteraction.Collide);

            var hitAny = false;
            for (var i = 0; i < count; i++)
            {
                var col = Buffer[i];
                if (col == null) continue;
                var hittable = col.GetComponentInParent<INetHittable>();
                if (hittable == null || !hittable.IsNetHittable) continue;
                hittable.OnNetHit(new NetHitInfo(center, source));
                hitAny = true;
            }

            return hitAny;
        }
    }
}
