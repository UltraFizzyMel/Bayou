using Bayou.Save;
using UnityEngine;

namespace Bayou.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class BayouCharacterMotor : MonoBehaviour
    {
        [Header("References (top-down / isometric)")]
        [Tooltip("Usually your isometric camera transform. Move input is mapped to the ground plane (XZ) using this transform's forward/right — not first-person look.")]
        [SerializeField] private Transform viewTransform;

        [Header("Input (New Input System)")]
#if ENABLE_INPUT_SYSTEM
        [Tooltip("Assign the Move action from your .inputactions asset. It is enabled automatically here unless you use PlayerInput on the same action (avoid duplicate wiring).")]
        [SerializeField] private UnityEngine.InputSystem.InputActionReference moveAction;

        /// <summary>Shared Move action — fishing attract uses the same binding.</summary>
        public UnityEngine.InputSystem.InputActionReference MoveAction => moveAction;
#endif

        [Header("Ground")]
        [SerializeField] private float maxSpeed = 6.0f;
        [SerializeField] private float acceleration = 30.0f;
        [SerializeField] private float braking = 40.0f;

        [Header("Water (heavier / slower)")]
        [SerializeField] private float waterSpeedMultiplier = 0.45f;
        [SerializeField] private float waterAccelerationMultiplier = 0.55f;
        [SerializeField] private float waterExtraLinearDamping = 4.0f;

        [Header("Turning")]
        [Tooltip("Character Y rotation follows movement direction — typical for 3D top-down.")]
        [SerializeField] private float turnSpeedDegPerSec = 720.0f;

        [Header("Grounding")]
        [SerializeField] private float groundProbeDistance = 0.2f;
        [SerializeField] private LayerMask groundMask = ~0;

        private Rigidbody rb;
        private BayouWaterSensor waterSensor;

        private Vector2 moveInput;
        private bool isGrounded;

        public Animator animator;

        /// <summary>Horizontal speed used by locomotion SFX.</summary>
        public float PlanarSpeed
        {
            get
            {
                if (rb == null) return 0f;
                var v = rb.linearVelocity;
                return new Vector3(v.x, 0f, v.z).magnitude;
            }
        }

        /// <summary>True when the player is holding move input.</summary>
        public bool HasMoveInput => moveInput.sqrMagnitude > 0.01f;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.constraints = RigidbodyConstraints.FreezeRotation;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

            waterSensor = GetComponent<BayouWaterSensor>();
        }

#if ENABLE_INPUT_SYSTEM
        private void OnEnable()
        {
            // Actions from an Input Actions asset start disabled; ReadValue is zero until enabled.
            if (moveAction != null && moveAction.action != null)
                moveAction.action.Enable();
        }

        private void OnDisable()
        {
            if (moveAction != null && moveAction.action != null)
                moveAction.action.Disable();
        }
#endif

        private void Update()
        {
            moveInput = Vector2.ClampMagnitude(
#if ENABLE_INPUT_SYSTEM
                Bayou.Input.BayouInput.ReadMove(moveAction),
#else
                Bayou.Input.BayouInput.ReadMove(null),
#endif
                1f
            );
        }

        private void FixedUpdate()
        {
            if (DialogueManager.GetInstance().dialogueIsPlaying)
            {
                return;
            }

            if (BonfireUIController.Active != null && BonfireUIController.Active.IsOpen)
            {
                return;
            }
            isGrounded = Physics.Raycast(
                origin: rb.position + Vector3.up * 0.05f,
                direction: Vector3.down,
                maxDistance: 0.05f + groundProbeDistance,
                layerMask: groundMask,
                queryTriggerInteraction: QueryTriggerInteraction.Ignore
            );

            var inWater = waterSensor != null && waterSensor.InWater;
            var speed = maxSpeed * (inWater ? waterSpeedMultiplier : 1f);
            var accel = acceleration * (inWater ? waterAccelerationMultiplier : 1f);

            var wishDir = GetWishDirection(moveInput);

            var vel = rb.linearVelocity;
            var planar = new Vector3(vel.x, 0f, vel.z);
            if (vel.x <= 0.001f  && vel.x >= -0.001f && vel.z <= 0.001f && vel.z >= -0.001f)
            {
                animator.SetBool("isMoving", false);
            }
            else
            {
                animator.SetBool("isMoving", true);
            }


            if (wishDir.sqrMagnitude > 0.0001f)
            {
                var desiredPlanar = wishDir * speed;
                var delta = desiredPlanar - planar;
                var maxDelta = accel * Time.fixedDeltaTime;
                var change = Vector3.ClampMagnitude(delta, maxDelta);

                rb.AddForce(new Vector3(change.x, 0f, change.z), ForceMode.VelocityChange);

                RotateTowards(wishDir);

            }
            else
            {
                // Braking: pull planar velocity toward zero.
                var maxDelta = braking * Time.fixedDeltaTime;
                var change = Vector3.ClampMagnitude(-planar, maxDelta);
                rb.AddForce(new Vector3(change.x, 0f, change.z), ForceMode.VelocityChange);
                
            }

            if (inWater)
            {
                // Extra damping in water makes it feel heavier.
                var damp = Mathf.Clamp01(waterExtraLinearDamping * Time.fixedDeltaTime);
                rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, Vector3.zero, damp * 0.15f);
            }

            // Keep y velocity governed by physics unless we're grounded and barely moving down.
            if (isGrounded && rb.linearVelocity.y < 0f)
            {
                rb.linearVelocity = new Vector3(rb.linearVelocity.x, -1f, rb.linearVelocity.z);
            }
        }

        private Vector3 GetWishDirection(Vector2 input)
        {
            if (input.sqrMagnitude < 0.0001f)
            {
               
                return Vector3.zero;
            }

            var forward = viewTransform != null ? viewTransform.forward : transform.forward;
            forward.y = 0f;
            forward = forward.sqrMagnitude < 0.0001f ? transform.forward : forward.normalized;

            var right = viewTransform != null ? viewTransform.right : transform.right;
            right.y = 0f;
            right = right.sqrMagnitude < 0.0001f ? transform.right : right.normalized;

            var wish = forward * input.y + right * input.x;
            wish.y = 0f;

            
            return wish.sqrMagnitude < 0.0001f ? Vector3.zero : wish.normalized;
        }

        private void RotateTowards(Vector3 dir)
        {
            if (dir.sqrMagnitude < 0.0001f) return;
            var targetRot = Quaternion.LookRotation(dir, Vector3.up);
            var maxStep = turnSpeedDegPerSec * Time.fixedDeltaTime;
            rb.MoveRotation(Quaternion.RotateTowards(rb.rotation, targetRot, maxStep));
        }
    }
}

