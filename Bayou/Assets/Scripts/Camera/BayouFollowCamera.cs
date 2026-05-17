using UnityEngine;

namespace Bayou.CameraControl
{
    /// <summary>
    /// Smooth world-space follow for top-down / isometric rigs: fixed offset from target, optional fixed rotation.
    /// Put on the Main Camera; assign the player transform.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BayouFollowCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;

        [Tooltip("Camera position = target position + this offset (world space).")]
        [SerializeField] private Vector3 worldOffset = new(0f, 14f, -12f);

        [Tooltip("Seconds to ease toward the desired position (lower = snappier).")]
        [SerializeField] private float positionSmoothTime = 0.18f;

        [Tooltip("Max speed the camera may move toward the target per second (0 = unlimited).")]
        [SerializeField] private float maxFollowSpeed = 80f;

        [Header("Rotation")]
        [SerializeField] private bool useFixedRotation = true;

        [SerializeField] private Vector3 fixedEulerAngles = new(55f, 0f, 0f);

        [Tooltip("If false, camera keeps its current rotation at start (useful if you aim manually in-editor).")]
        [SerializeField] private bool applyFixedRotationOnEnable = true;

        private Vector3 _smoothVelocity;

        private void OnEnable()
        {
            if (applyFixedRotationOnEnable && useFixedRotation)
                transform.rotation = Quaternion.Euler(fixedEulerAngles);
        }

        private void LateUpdate()
        {
            if (target == null)
                return;

            var desired = target.position + worldOffset;

            if (positionSmoothTime <= 0f)
            {
                transform.position = desired;
            }
            else
            {
                transform.position = Vector3.SmoothDamp(
                    transform.position,
                    desired,
                    ref _smoothVelocity,
                    positionSmoothTime,
                    maxFollowSpeed > 0f ? maxFollowSpeed : Mathf.Infinity,
                    Time.deltaTime
                );
            }

            if (useFixedRotation)
                transform.rotation = Quaternion.Euler(fixedEulerAngles);
        }

        /// <summary>Jump to ideal placement immediately (e.g. after teleport).</summary>
        public void SnapToTarget()
        {
            if (target == null) return;
            transform.position = target.position + worldOffset;
            _smoothVelocity = Vector3.zero;
            if (useFixedRotation)
                transform.rotation = Quaternion.Euler(fixedEulerAngles);
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }
    }
}
