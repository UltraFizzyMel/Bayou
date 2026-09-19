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

        [Header("Water — wade")]
        [SerializeField] private float waterSpeedMultiplier = 0.45f;
        [SerializeField] private float waterAccelerationMultiplier = 0.55f;
        [SerializeField] private float waterExtraLinearDamping = 4.0f;

        [Header("Water — swim")]
        [SerializeField] private float swimSpeedMultiplier = 0.7f;
        [SerializeField] private float swimAccelerationMultiplier = 0.75f;
        [SerializeField] private float swimRiseSpeed = 6f;
        [SerializeField] private float swimCapsuleHeight = 0.55f;
        [SerializeField] private float swimBoneMoveLimit = 0.85f;

        [Header("Turning")]
        [Tooltip("Character Y rotation follows movement direction — typical for 3D top-down.")]
        [SerializeField] private float turnSpeedDegPerSec = 720.0f;

        [Header("Grounding")]
        [SerializeField] private float groundProbeDistance = 0.2f;
        [SerializeField] private LayerMask groundMask = ~0;

        private Rigidbody rb;
        private BayouWaterSensor waterSensor;
        private CapsuleCollider bodyCapsule;

        private Vector2 moveInput;
        private bool isGrounded;
        private bool _hasInWaterParam;
        private bool _hasInDeepWaterParam;
        private bool _gravityCached = true;
        private bool _capsuleCached;
        private float _standCapsuleHeight = 2f;
        private float _standCapsuleRadius = 0.5f;
        private Vector3 _standCapsuleCenter;
        private Transform _visualRoot;
        private Vector3 _visualBindLocalPos;
        private Vector3 _visualBindLocalScale;
        private Transform[] _bindBones;
        private Vector3[] _bindBonePos;
        private Vector3[] _bindBoneScale;
        private bool _hasHurtTrigger;
        private float _stunUntil;

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

        /// <summary>True during a hit stagger — locomotion and fishing should idle.</summary>
        public bool IsStunned => Time.time < _stunUntil;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.constraints = RigidbodyConstraints.FreezeRotation;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

            waterSensor = GetComponent<BayouWaterSensor>();
            if (waterSensor == null)
                waterSensor = gameObject.AddComponent<BayouWaterSensor>();
            bodyCapsule = GetComponent<CapsuleCollider>();
            CacheStandCapsule();
            CacheAnimatorParams();
            PlayerLocator.Bind(gameObject);
            PlayerOutline.EnsureOn(gameObject);
            PlayerHealth.EnsureOn(gameObject);
        }

        private void CacheStandCapsule()
        {
            if (bodyCapsule == null || _capsuleCached) return;
            _standCapsuleHeight = bodyCapsule.height;
            _standCapsuleRadius = bodyCapsule.radius;
            _standCapsuleCenter = bodyCapsule.center;
            _capsuleCached = true;
        }

        private void CacheAnimatorParams()
        {
            _hasInWaterParam = false;
            _hasInDeepWaterParam = false;
            _hasHurtTrigger = false;
            if (animator == null) return;
            var parms = animator.parameters;
            for (var i = 0; i < parms.Length; i++)
            {
                if (parms[i].name == "hurt" && parms[i].type == AnimatorControllerParameterType.Trigger)
                    _hasHurtTrigger = true;
                if (parms[i].type != AnimatorControllerParameterType.Bool) continue;
                if (parms[i].name == "inWater") _hasInWaterParam = true;
                else if (parms[i].name == "inDeepWater") _hasInDeepWaterParam = true;
            }
            CacheBindPose();
        }

        public void ApplyHitReaction(Vector3 planarVelocity, float stunSeconds)
        {
            _stunUntil = Time.time + Mathf.Max(0.05f, stunSeconds);
            if (rb == null) return;
            planarVelocity.y = 0f;
            if (planarVelocity.sqrMagnitude < 0.0001f) return;
            var v = rb.linearVelocity;
            rb.linearVelocity = new Vector3(planarVelocity.x, Mathf.Max(v.y, 0.35f), planarVelocity.z);
        }

        public void Teleport(Vector3 position, Quaternion rotation)
        {
            _stunUntil = 0f;
            transform.SetPositionAndRotation(position, rotation);
            if (rb == null) return;
            rb.position = position;
            rb.rotation = rotation;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        public void PlayHurtAnimation()
        {
            if (animator != null && _hasHurtTrigger)
                animator.SetTrigger("hurt");
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
            if (rb != null && _gravityCached)
                rb.useGravity = true;
            RestoreStandCapsule();
        }

        private void OnDestroy()
        {
            PlayerLocator.ClearIf(this);
        }
#endif

        private void Start()
        {
            CacheAnimatorParams();
        }

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

            var swimming = waterSensor != null && waterSensor.IsSwimming;
            var wading = waterSensor != null && waterSensor.IsWading;

            if (IsStunned)
            {
                ApplySwimBuoyancy(swimming);
                ApplyWaterAnimator(wading, swimming);
                if (animator != null)
                    animator.SetBool("isMoving", PlanarSpeed > 0.15f);
                return;
            }

            var speedMul = swimming ? swimSpeedMultiplier : (wading ? waterSpeedMultiplier : 1f);
            var accelMul = swimming ? swimAccelerationMultiplier : (wading ? waterAccelerationMultiplier : 1f);
            var speed = maxSpeed * speedMul;
            var accel = acceleration * accelMul;

            var wishDir = GetWishDirection(moveInput);

            var vel = rb.linearVelocity;
            var planar = new Vector3(vel.x, 0f, vel.z);
            if (animator != null)
            {
                if (vel.x <= 0.001f && vel.x >= -0.001f && vel.z <= 0.001f && vel.z >= -0.001f)
                    animator.SetBool("isMoving", false);
                else
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

            if (wading && !swimming)
            {
                var damp = Mathf.Clamp01(waterExtraLinearDamping * Time.fixedDeltaTime);
                rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, Vector3.zero, damp * 0.15f);
            }

            ApplySwimBuoyancy(swimming);
            ApplyWaterAnimator(wading, swimming);

            if (!swimming && isGrounded && rb.linearVelocity.y < 0f)
            {
                rb.linearVelocity = new Vector3(rb.linearVelocity.x, -1f, rb.linearVelocity.z);
            }
        }

        private void LateUpdate()
        {
            if (waterSensor != null && waterSensor.IsSwimming)
                StabilizeSwimPose();
        }

        private void ApplySwimBuoyancy(bool swimming)
        {
            if (swimming)
            {
                if (rb.useGravity)
                {
                    _gravityCached = true;
                    rb.useGravity = false;
                }

                ApplySwimCapsule(true);

                var vel = rb.linearVelocity;
                var surface = waterSensor != null ? waterSensor.WaterSurfaceY : rb.position.y;
                var belowSurface = surface - rb.position.y;

                // Deep water: rise toward the surface. Shallow inner-pond swim: don't sink.
                if (belowSurface > 1.05f)
                    vel.y = (surface - 0.35f - rb.position.y) * swimRiseSpeed;
                else if (vel.y < 0f)
                    vel.y = 0f;

                rb.linearVelocity = vel;
            }
            else
            {
                ApplySwimCapsule(false);
                if (!rb.useGravity && _gravityCached)
                    rb.useGravity = true;
            }
        }

        private void ApplySwimCapsule(bool swimming)
        {
            if (bodyCapsule == null) return;
            CacheStandCapsule();
            if (swimming)
            {
                bodyCapsule.height = Mathf.Max(0.35f, swimCapsuleHeight);
                bodyCapsule.center = Vector3.zero;
            }
            else
            {
                RestoreStandCapsule();
            }
        }

        private void RestoreStandCapsule()
        {
            if (bodyCapsule == null || !_capsuleCached) return;
            bodyCapsule.height = _standCapsuleHeight;
            bodyCapsule.radius = _standCapsuleRadius;
            bodyCapsule.center = _standCapsuleCenter;
        }

        private void ApplyWaterAnimator(bool wading, bool swimming)
        {
            if (animator == null) return;
            if (_hasInWaterParam)
                animator.SetBool("inWater", wading || swimming);
            if (_hasInDeepWaterParam)
                animator.SetBool("inDeepWater", swimming);
        }

        private void CacheBindPose()
        {
            if (animator == null) return;
            _visualRoot = animator.transform;
            _visualBindLocalPos = _visualRoot.localPosition;
            _visualBindLocalScale = _visualRoot.localScale;
            _bindBones = _visualRoot.GetComponentsInChildren<Transform>(true);
            _bindBonePos = new Vector3[_bindBones.Length];
            _bindBoneScale = new Vector3[_bindBones.Length];
            for (var i = 0; i < _bindBones.Length; i++)
            {
                _bindBonePos[i] = _bindBones[i].localPosition;
                _bindBoneScale[i] = _bindBones[i].localScale;
            }
        }

        private void StabilizeSwimPose()
        {
            if (_visualRoot == null || _bindBones == null) return;

            // Swim clips were authored away from the standing root. Keep the visual
            // where it was placed, and stop bone translation/scale from rubber-banding
            // the mesh between planted feet and a distant swim pose.
            _visualRoot.localPosition = _visualBindLocalPos;
            _visualRoot.localScale = _visualBindLocalScale;

            var limit = Mathf.Max(0.15f, swimBoneMoveLimit);
            var limitSq = limit * limit;
            for (var i = 0; i < _bindBones.Length; i++)
            {
                var bone = _bindBones[i];
                if (bone == null) continue;
                bone.localScale = _bindBoneScale[i];
                if (bone == _visualRoot) continue;

                var delta = bone.localPosition - _bindBonePos[i];
                if (bone.parent == _visualRoot)
                {
                    bone.localPosition = _bindBonePos[i];
                    continue;
                }

                if (delta.sqrMagnitude > limitSq)
                    bone.localPosition = _bindBonePos[i] + Vector3.ClampMagnitude(delta, limit);
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

