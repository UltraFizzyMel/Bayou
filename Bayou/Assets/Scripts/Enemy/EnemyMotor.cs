using UnityEngine;

namespace Bayou.Enemy
{
    /// <summary>
    /// Simple planar Rigidbody motor for hostiles (works without a baked NavMesh).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class EnemyMotor : MonoBehaviour
    {
        [SerializeField] private float maxSpeed = 3.6f;
        [SerializeField] private float acceleration = 22f;
        [SerializeField] private float braking = 28f;
        [SerializeField] private float turnSpeedDegPerSec = 480f;

        private Rigidbody _rb;
        private Vector3 _wishDir;
        private bool _stop;

        public float MaxSpeed
        {
            get => maxSpeed;
            set => maxSpeed = Mathf.Max(0.1f, value);
        }

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.constraints = RigidbodyConstraints.FreezeRotation;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        }

        public void SetWishDirection(Vector3 worldDir)
        {
            worldDir.y = 0f;
            _wishDir = worldDir.sqrMagnitude > 0.0001f ? worldDir.normalized : Vector3.zero;
            _stop = false;
        }

        public void Stop()
        {
            _wishDir = Vector3.zero;
            _stop = true;
        }

        private void FixedUpdate()
        {
            var vel = _rb.linearVelocity;
            var planar = new Vector3(vel.x, 0f, vel.z);

            if (!_stop && _wishDir.sqrMagnitude > 0.0001f)
            {
                var desired = _wishDir * maxSpeed;
                var delta = desired - planar;
                var change = Vector3.ClampMagnitude(delta, acceleration * Time.fixedDeltaTime);
                _rb.AddForce(new Vector3(change.x, 0f, change.z), ForceMode.VelocityChange);

                var targetRot = Quaternion.LookRotation(_wishDir, Vector3.up);
                _rb.MoveRotation(Quaternion.RotateTowards(
                    _rb.rotation,
                    targetRot,
                    turnSpeedDegPerSec * Time.fixedDeltaTime));
            }
            else
            {
                var change = Vector3.ClampMagnitude(-planar, braking * Time.fixedDeltaTime);
                _rb.AddForce(new Vector3(change.x, 0f, change.z), ForceMode.VelocityChange);
            }
        }
    }
}
