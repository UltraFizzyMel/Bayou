using UnityEngine;

namespace Bayou.Player
{
    /// <summary>
    /// 8-way cardinal facing helpers (N, NE, E, SE, S, SW, W, NW) for fishing aim and AI.
    /// </summary>
    public static class BayouFacing
    {
        public const float CardinalStepDegrees = 45f;

        /// <summary>Flattened forward of <paramref name="t"/>, snapped to the nearest of 8 world cardinals.</summary>
        public static Vector3 GetCardinalForward8(Transform t)
        {
            if (t == null) return Vector3.forward;
            return SnapToCardinal8(t.forward);
        }

        /// <summary>Snap any world direction to the nearest of 8 XZ cardinals.</summary>
        public static Vector3 SnapToCardinal8(Vector3 worldDirection)
        {
            var flat = worldDirection;
            flat.y = 0f;
            if (flat.sqrMagnitude < 1e-6f)
                return Vector3.forward;

            var yaw = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;
            var snapped = Mathf.Round(yaw / CardinalStepDegrees) * CardinalStepDegrees;
            return Quaternion.Euler(0f, snapped, 0f) * Vector3.forward;
        }

        /// <summary>Yaw in degrees for a cardinal direction (0 = +Z / north in Unity).</summary>
        public static float GetCardinalYawDegrees(Transform t)
        {
            var dir = GetCardinalForward8(t);
            return Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        }
    }
}
