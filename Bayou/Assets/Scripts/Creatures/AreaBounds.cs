using UnityEngine;

namespace Bayou.Creatures
{
    /// <summary>Circle (XZ) or axis-aligned box used to keep crocs / wanderers inside a zone.</summary>
    public enum AreaBoundsShape
    {
        Circle,
        Box
    }

    [DisallowMultipleComponent]
    public sealed class AreaBounds : MonoBehaviour
    {
        [SerializeField] private AreaBoundsShape shape = AreaBoundsShape.Circle;
        [SerializeField] private float radius = 8f;
        [SerializeField] private Vector3 boxSize = new(12f, 4f, 12f);

        public Vector3 Center => transform.position;
        public float Radius => radius;

        public void ConfigureCircle(float newRadius)
        {
            shape = AreaBoundsShape.Circle;
            radius = Mathf.Max(0.5f, newRadius);
        }

        public void ConfigureBox(Vector3 size)
        {
            shape = AreaBoundsShape.Box;
            boxSize = size;
        }

        public bool Contains(Vector3 worldPoint)
        {
            var local = worldPoint - Center;
            local.y = 0f;
            if (shape == AreaBoundsShape.Circle)
                return local.sqrMagnitude <= radius * radius;

            var half = boxSize * 0.5f;
            return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.z) <= half.z;
        }

        public Vector3 ClampInside(Vector3 worldPoint)
        {
            var local = worldPoint - Center;
            var y = worldPoint.y;
            local.y = 0f;

            if (shape == AreaBoundsShape.Circle)
            {
                if (local.sqrMagnitude > radius * radius)
                    local = local.normalized * radius;
            }
            else
            {
                var half = boxSize * 0.5f;
                local.x = Mathf.Clamp(local.x, -half.x, half.x);
                local.z = Mathf.Clamp(local.z, -half.z, half.z);
            }

            var result = Center + local;
            result.y = y;
            return result;
        }

        public Vector3 RandomPointInside()
        {
            Vector3 offset;
            if (shape == AreaBoundsShape.Circle)
            {
                var r = radius * Mathf.Sqrt(Random.value);
                var a = Random.value * Mathf.PI * 2f;
                offset = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
            }
            else
            {
                var half = boxSize * 0.5f;
                offset = new Vector3(
                    Random.Range(-half.x, half.x),
                    0f,
                    Random.Range(-half.z, half.z));
            }

            var p = Center + offset;
            p.y = Center.y;
            return p;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.85f, 0.45f, 0.35f);
            if (shape == AreaBoundsShape.Circle)
            {
                const int segs = 32;
                var prev = Center + new Vector3(radius, 0.1f, 0f);
                for (var i = 1; i <= segs; i++)
                {
                    var t = (i / (float)segs) * Mathf.PI * 2f;
                    var next = Center + new Vector3(Mathf.Cos(t) * radius, 0.1f, Mathf.Sin(t) * radius);
                    Gizmos.DrawLine(prev, next);
                    prev = next;
                }
            }
            else
            {
                Gizmos.matrix = Matrix4x4.TRS(Center, Quaternion.identity, Vector3.one);
                Gizmos.DrawWireCube(Vector3.up * 0.1f, new Vector3(boxSize.x, 0.05f, boxSize.z));
                Gizmos.matrix = Matrix4x4.identity;
            }
        }
#endif
    }
}
