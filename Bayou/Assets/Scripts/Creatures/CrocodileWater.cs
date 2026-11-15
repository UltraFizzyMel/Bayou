using System.Collections.Generic;
using Bayou.Environment;
using UnityEngine;

namespace Bayou.Creatures
{
    /// <summary>
    /// Keeps crocodiles in water. Destinations on land are rejected.
    /// If a step still leaves the water, the body slides back — it is not teleported, and stun is left alone.
    /// </summary>
    public static class CrocodileWater
    {
        private const float Nearby = 22f;
        private static readonly Dictionary<int, Vector3> LastSwim = new();
        private static WaterVolume[] _volumes = System.Array.Empty<WaterVolume>();
        private static float _cacheUntil;

        public static bool IsSwimmable(Vector3 world)
        {
            var volumes = Volumes();
            for (var i = 0; i < volumes.Length; i++)
            {
                var volume = volumes[i];
                if (volume == null || !Covers(volume, world)) continue;
                var depth = volume.Evaluate(world);
                if (depth == WaterDepthLevel.Wade || depth == WaterDepthLevel.Swim)
                    return true;
            }

            return false;
        }

        public static bool HasWaterNear(Vector3 world) => Nearest(world, Nearby) != null;

        /// <summary>True when this step would leave water that is actually beside the croc.</summary>
        public static bool RejectStep(Vector3 from, Vector3 to)
        {
            if (!HasWaterNear(from))
                return false;
            if (!IsSwimmable(from))
                return false;
            return !IsSwimmable(to);
        }

        public static Vector3 PickSwimPoint(Vector3 near)
        {
            var volume = Nearest(near, Nearby);
            if (volume == null)
                return near;

            var col = volume.TriggerCollider;
            if (col == null)
                return near;

            var bounds = col.bounds;
            for (var attempt = 0; attempt < 8; attempt++)
            {
                var point = new Vector3(
                    Random.Range(bounds.min.x + 0.6f, bounds.max.x - 0.6f),
                    volume.SurfaceY - 0.35f,
                    Random.Range(bounds.min.z + 0.6f, bounds.max.z - 0.6f));
                if (IsSwimmable(point))
                    return point;
            }

            var center = bounds.center;
            center.y = volume.SurfaceY - 0.35f;
            return IsSwimmable(center) ? center : near;
        }

        public static void PullBack(Transform body)
        {
            if (body == null) return;
            var id = body.GetInstanceID();
            if (IsSwimmable(body.position))
            {
                LastSwim[id] = body.position;
                return;
            }

            if (!LastSwim.TryGetValue(id, out var last))
            {
                if (!HasWaterNear(body.position))
                    return;
                last = PickSwimPoint(body.position);
                if (!IsSwimmable(last))
                    return;
            }

            var next = Vector3.MoveTowards(body.position, last, 3.2f * Time.deltaTime);
            body.position = next;
        }

        private static bool Covers(WaterVolume volume, Vector3 world)
        {
            var col = volume.TriggerCollider;
            if (col == null) return false;
            var bounds = col.bounds;
            return world.x >= bounds.min.x && world.x <= bounds.max.x &&
                   world.z >= bounds.min.z && world.z <= bounds.max.z;
        }

        private static WaterVolume Nearest(Vector3 world, float maxDistance)
        {
            var volumes = Volumes();
            WaterVolume best = null;
            var bestSq = maxDistance * maxDistance;
            for (var i = 0; i < volumes.Length; i++)
            {
                var volume = volumes[i];
                if (volume == null) continue;
                var col = volume.TriggerCollider;
                if (col == null) continue;
                var flat = col.bounds.center - world;
                flat.y = 0f;
                var sq = flat.sqrMagnitude;
                if (sq > bestSq) continue;
                bestSq = sq;
                best = volume;
            }

            return best;
        }

        private static WaterVolume[] Volumes()
        {
            if (Time.unscaledTime < _cacheUntil && _volumes != null)
                return _volumes;
            _cacheUntil = Time.unscaledTime + 1.2f;
            _volumes = Object.FindObjectsByType<WaterVolume>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            return _volumes;
        }
    }
}
