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
        Stunned
    }

    public readonly struct NetHitInfo
    {
        public readonly Vector3 HitPoint;
        public readonly NetHitSource Source;

        public NetHitInfo(Vector3 hitPoint, NetHitSource source)
        {
            HitPoint = hitPoint;
            Source = source;
        }
    }

    /// <summary>Anything the hand net / thrown net can affect (snakes, crocs, etc.).</summary>
    public interface INetHittable
    {
        bool IsNetHittable { get; }
        NetHitResult OnNetHit(NetHitInfo info);
    }
}
