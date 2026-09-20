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
        private bool _swimSnapArmed = true;
        private float _deepWaterAnimLatch;

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
            _standCapsuleRadius = Mathf.Min(bodyCapsule.radius, 0.32f);
            _standCapsuleCenter = bodyCapsule.center;
            bodyCapsule.radius = _standCapsuleRadius;
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
                animator.SetBool("isMoving", HasMoveInput || planar.magnitude > 0.2f);


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
            KeepFromFallingThroughWater(wading, swimming);
            ResolveStaticOverlaps();

            if (!swimming && isGrounded && rb.linearVelocity.y < 0f)
            {
                rb.linearVelocity = new Vector3(rb.linearVelocity.x, -1f, rb.linearVelocity.z);
            }
        }

        private void LateUpdate()
        {
            if (animator == null)
                animator = GetComponentInChildren<Animator>();
            if (animator != null && _visualRoot == null)
                CacheAnimatorParams();

            SyncLocomotionAnimSpeed();
            LockBindScale();

            if (waterSensor == null) return;
            if (!waterSensor.IsSwimming)
            {
                _swimSnapArmed = true;
                return;
            }

            SnapToSwimStateIfNeeded();
            StabilizeSwimPose();
        }

        private void SyncLocomotionAnimSpeed()
        {
            if (animator == null) return;

            var moving = !IsStunned && (HasMoveInput || PlanarSpeed > 0.2f);
            animator.SetBool("isMoving", moving);

            if (animator.IsInTransition(0))
                return;

            var info = animator.GetCurrentAnimatorStateInfo(0);
            if (IsSwimState(info))
            {
                animator.speed = 1f;
                return;
            }

            var loco = info.IsName("Armature|Walking") ||
                       info.IsName("Armature|Wading") ||
                       info.IsName("Armature|WalkingHoldingRod") ||
                       info.IsName("Armature|WalkingWithLantern");

            if (!loco || !moving)
            {
                animator.speed = 1f;
                return;
            }

            var reference = Mathf.Max(0.5f, maxSpeed);
            animator.speed = Mathf.Clamp(PlanarSpeed / reference, 0.4f, 1.2f);
        }

        private static readonly Collider[] OverlapBuffer = new Collider[12];

        private void ResolveStaticOverlaps()
        {
            if (bodyCapsule == null || rb == null) return;

            var p = rb.position + bodyCapsule.center;
            var height = Mathf.Max(bodyCapsule.height, bodyCapsule.radius * 2f);
            var pointOff = Vector3.up * (height * 0.5f - bodyCapsule.radius);
            var count = Physics.OverlapCapsuleNonAlloc(
                p + pointOff, p - pointOff, bodyCapsule.radius * 0.92f,
                OverlapBuffer, groundMask, QueryTriggerInteraction.Ignore);
            var push = Vector3.zero;
            for (var i = 0; i < count; i++)
            {
                var col = OverlapBuffer[i];
                if (col == null || col == bodyCapsule) continue;
                if (col is TerrainCollider) continue;
                if (col.transform.IsChildOf(transform)) continue;
                if (col.GetComponentInParent<Bayou.Environment.WaterVolume>() != null)
                    continue;
                if (Physics.ComputePenetration(
                        bodyCapsule, rb.position, rb.rotation,
                        col, col.transform.position, col.transform.rotation,
                        out var dir, out var dist))
                {
                    if (dir.y > 0.7f) continue;
                    dir.y = 0f;
                    push += dir * dist;
                }
            }

            push.y = 0f;
            if (push.sqrMagnitude < 0.0001f) return;
            rb.MovePosition(rb.position + Vector3.ClampMagnitude(push, 0.35f));
        }

        private void KeepFromFallingThroughWater(bool wading, bool swimming)
        {
            if (waterSensor == null || (!wading && !swimming) || rb == null)
                return;

            var minY = swimming
                ? waterSensor.SwimHoldY - 0.12f
                : waterSensor.WaterSurfaceY - 0.55f;
            if (rb.position.y >= minY)
                return;

            var p = rb.position;
            p.y = minY;
            rb.position = p;
            var vel = rb.linearVelocity;
            if (vel.y < 0f)
                rb.linearVelocity = new Vector3(vel.x, 0f, vel.z);
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
                var targetY = waterSensor != null ? waterSensor.SwimHoldY : rb.position.y;
                var dy = targetY - rb.position.y;
                if (Mathf.Abs(dy) < 0.04f)
                    vel.y = 0f;
                else
                    vel.y = Mathf.Clamp(dy * 3.2f, -2.2f, 2.2f);

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
            if (swimming)
                _deepWaterAnimLatch = Time.time + 0.35f;
            var deep = swimming || Time.time < _deepWaterAnimLatch;
            if (_hasInWaterParam)
                animator.SetBool("inWater", wading || swimming || deep);
            if (_hasInDeepWaterParam)
                animator.SetBool("inDeepWater", deep);
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
                var bone = _bindBones[i];
                if (bone == null || IsHeldProp(bone)) continue;
                _bindBonePos[i] = bone.localPosition;
                _bindBoneScale[i] = bone.localScale;
            }
        }

        private void LockBindScale()
        {
            if (_visualRoot == null || _bindBones == null) return;
            _visualRoot.localScale = _visualBindLocalScale;
            for (var i = 0; i < _bindBones.Length; i++)
            {
                var bone = _bindBones[i];
                if (bone == null || IsHeldProp(bone)) continue;
                bone.localScale = _bindBoneScale[i];
            }
        }

        private static bool IsHeldProp(Transform t)
        {
            for (var p = t; p != null; p = p.parent)
            {
                var n = p.name;
                if (n.StartsWith("HeldRod", System.StringComparison.OrdinalIgnoreCase) ||
                    n.StartsWith("HeldNet", System.StringComparison.OrdinalIgnoreCase) ||
                    n.StartsWith("HeldLantern", System.StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private void SnapToSwimStateIfNeeded()
        {
            if (animator == null) return;
            if (animator.IsInTransition(0)) return;

            var info = animator.GetCurrentAnimatorStateInfo(0);
            if (IsSwimState(info))
            {
                _swimSnapArmed = false;
                return;
            }

            if (!_swimSnapArmed) return;
            _swimSnapArmed = false;

            string clip;
            if (HasAnimatorBool("isHoldingLantern") && animator.GetBool("isHoldingLantern"))
                clip = "Armature|SwimmingHoldingLight";
            else if (HasAnimatorBool("isHoldingRod") && animator.GetBool("isHoldingRod"))
                clip = "Armature|SwimmingHoldingRod";
            else if (HasAnimatorBool("isMoving") && animator.GetBool("isMoving"))
                clip = "Armature|Swimming";
            else
                clip = "Armature|IdleSwimming";

            animator.CrossFadeInFixedTime(clip, 0.2f);
        }

        private static bool IsSwimState(AnimatorStateInfo info) =>
            info.IsName("Armature|Swimming") ||
            info.IsName("Armature|IdleSwimming") ||
            info.IsName("Armature|SwimmingHoldingRod") ||
            info.IsName("Armature|SwimmingHoldingLight");

        private bool HasAnimatorBool(string name)
        {
            if (animator == null) return false;
            var parms = animator.parameters;
            for (var i = 0; i < parms.Length; i++)
            {
                if (parms[i].type == AnimatorControllerParameterType.Bool && parms[i].name == name)
                    return true;
            }
            return false;
        }

        private void StabilizeSwimPose()
        {
            if (_visualRoot == null || _bindBones == null) return;

            // Only kill scale stretch. Writing bone positions every LateUpdate
            // fought the swim clip and made held items jitter in deep water.
            _visualRoot.localScale = _visualBindLocalScale;
            for (var i = 0; i < _bindBones.Length; i++)
            {
                var bone = _bindBones[i];
                if (bone == null || IsHeldProp(bone)) continue;
                bone.localScale = _bindBoneScale[i];
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

