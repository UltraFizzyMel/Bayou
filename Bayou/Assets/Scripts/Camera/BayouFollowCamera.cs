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

        [Tooltip("Distance from the player. The offset is rebuilt from the pitch so the player stays centered.")]
        [SerializeField] private float followDistance = 24f;

        [Tooltip("How close the camera moves when a conversation starts. 1 = no change.")]
        [SerializeField] private float dialogueZoom = 0.78f;

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
        private Vector3 _punch;
        private float _zoom = 1f;

        private void OnEnable()
        {
            if (applyFixedRotationOnEnable && useFixedRotation)
                transform.rotation = Quaternion.Euler(fixedEulerAngles);
        }

        private void LateUpdate()
        {
            if (target == null)
                return;

            var talking = false;
            var dialogue = DialogueManager.GetInstance();
            if (dialogue != null)
                talking = dialogue.dialogueIsPlaying;
            var zoomTarget = talking ? Mathf.Clamp(dialogueZoom, 0.55f, 1f) : 1f;
            var dt = Time.unscaledDeltaTime;
            _zoom = Mathf.Lerp(_zoom, zoomTarget, 1f - Mathf.Exp(-5f * dt));

            var desired = target.position + FollowOffset(_zoom);
            _punch = Vector3.Lerp(_punch, Vector3.zero, 1f - Mathf.Exp(-10f * dt));
            desired += _punch;

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
                    dt
                );
            }

            if (useFixedRotation)
                transform.rotation = Quaternion.Euler(fixedEulerAngles);
        }

        /// <summary>Jump to ideal placement immediately (e.g. after teleport).</summary>
        public void SnapToTarget()
        {
            if (target == null) return;
            _zoom = 1f;
            transform.position = target.position + FollowOffset(_zoom);
            _smoothVelocity = Vector3.zero;
            _punch = Vector3.zero;
            if (useFixedRotation)
                transform.rotation = Quaternion.Euler(fixedEulerAngles);
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }

        /// <summary>
        /// Offset along the fixed pitch so the view ray through the screen center hits the player.
        /// </summary>
        private Vector3 FollowOffset(float zoom)
        {
            var distance = followDistance > 1f
                ? followDistance
                : new Vector2(worldOffset.y, worldOffset.z).magnitude;
            distance = Mathf.Max(6f, distance) * Mathf.Clamp(zoom, 0.55f, 1.25f);
            var pitch = fixedEulerAngles.x * Mathf.Deg2Rad;
            var offset = new Vector3(0f, Mathf.Sin(pitch) * distance, -Mathf.Cos(pitch) * distance);
            return Quaternion.Euler(0f, fixedEulerAngles.y, 0f) * offset;
        }

        /// <summary>Short combat punch so a connecting hit is readable on an isometric rig.</summary>
        public void Punch(Vector3 worldImpulse)
        {
            _punch += worldImpulse;
            if (_punch.sqrMagnitude > 0.45f * 0.45f)
                _punch = _punch.normalized * 0.45f;
        }
    }
}
