using System.Collections.Generic;
using Bayou.Creatures;
using UnityEngine;

namespace Bayou.Fishing
{
    /// <summary>Shared overlap / facing-cone swing for net / rod melee.</summary>
    internal static class ToolMelee
    {
        private static readonly Collider[] Buffer = new Collider[32];
        private static readonly List<int> HitIds = new(16);

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
                hittable.OnNetHit(new NetHitInfo(center, source, damage: 1f));
                hitAny = true;
            }

            return hitAny;
        }

        public static void BeginSweep() => HitIds.Clear();

        /// <summary>
        /// Hit living creatures in a moving blade slice of a forward arc.
        /// Call <see cref="BeginSweep"/> once per swing so each creature is hit at most once.
        /// </summary>
        public static bool TryHitSweepSlice(
            Vector3 origin,
            Vector3 forward,
            float range,
            float sliceCenterDegrees,
            float sliceWidthDegrees,
            float guaranteedRadius,
            NetHitSource source,
            List<CreatureController> hitsThisSlice)
        {
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-6f)
                forward = Vector3.forward;
            forward.Normalize();

            range = Mathf.Max(0.35f, range);
            guaranteedRadius = Mathf.Clamp(guaranteedRadius, 0.15f, range);
            var rangeSq = range * range;
            var guaranteedSq = guaranteedRadius * guaranteedRadius;
            var sliceHalf = Mathf.Max(8f, sliceWidthDegrees) * 0.5f;
            var hitAny = false;

            var creatures = CreatureController.Living;
            for (var i = 0; i < creatures.Count; i++)
            {
                var creature = creatures[i];
                if (creature == null || !creature.isActiveAndEnabled || !creature.IsNetHittable)
                    continue;

                var id = creature.GetInstanceID();
                if (HitIds.Contains(id))
                    continue;

                var aim = creature.ClosestPointFrom(origin);
                var to = aim - origin;
                to.y = 0f;
                var distSq = to.sqrMagnitude;
                if (distSq > rangeSq)
                    continue;

                var inBlade = distSq <= guaranteedSq;
                if (!inBlade && distSq > 0.0001f)
                {
                    var signed = Vector3.SignedAngle(forward, to, Vector3.up);
                    inBlade = Mathf.Abs(Mathf.DeltaAngle(sliceCenterDegrees, signed)) <= sliceHalf;
                }

                if (!inBlade)
                    continue;

                HitIds.Add(id);
                creature.OnNetHit(new NetHitInfo(origin, source, damage: 1f));
                hitsThisSlice?.Add(creature);
                hitAny = true;
            }

            return hitAny;
        }
    }
}
