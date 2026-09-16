using UnityEngine;

namespace Bayou.Creatures
{
    public enum NetHitSource
    {
        HandNet,
        ThrownNet,
        /// <summary>Hand net used as a melee swing while pursued.</summary>
        MeleeNet,
        /// <summary>Fishing rod used as a melee swing while pursued.</summary>
        MeleeRod
    }

    public enum NetHitResult
    {
        Ignored,
        Caught,
        Stunned,
        Damaged,
        Killed
    }

    public readonly struct NetHitInfo
    {
        public readonly Vector3 HitPoint;
        public readonly NetHitSource Source;
        public readonly float Damage;

        public NetHitInfo(Vector3 hitPoint, NetHitSource source, float damage = 0f)
        {
            HitPoint = hitPoint;
            Source = source;
            Damage = damage;
        }

        public bool IsMelee => Source == NetHitSource.MeleeNet || Source == NetHitSource.MeleeRod;
    }

    /// <summary>Anything the hand net / thrown net can affect (snakes, crocs, etc.).</summary>
    public interface INetHittable
    {
        bool IsNetHittable { get; }
        NetHitResult OnNetHit(NetHitInfo info);
    }
}
