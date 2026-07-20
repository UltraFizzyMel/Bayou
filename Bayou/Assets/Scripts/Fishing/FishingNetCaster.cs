#if !ENABLE_INPUT_SYSTEM
#error FishingNetCaster requires the New Input System (ENABLE_INPUT_SYSTEM). Project Settings > Player > Active Input Handling must include Input System, or add scripting define.
#endif

using UnityEngine;
using UnityEngine.InputSystem;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Bayou.Fishing
{
    public enum FishingCastPhase
    {
        Idle,
        DirectionSweep,
        ChargingTrajectory
    }

    [DisallowMultipleComponent]
    public sealed class FishingNetCaster : MonoBehaviour
    {
        [Header("References (top-down / isometric)")]
        [SerializeField] private Transform castOrigin;
        [Tooltip("Raise spawn above the player root so the net doesn't instantly hit the ground.")]
        [SerializeField] private float castHeightOffset = 1.45f;
        [Tooltip("Isometric camera: forward is flattened to XZ for aim.")]
        [SerializeField] private Transform aimTransform;
        [SerializeField] private FishingNetProjectile netPrefab;
        [SerializeField] private LineRenderer trajectoryLine;

        [Tooltip("If no LineRenderer is assigned, create runtime trajectory preview.")]
        [SerializeField] private bool autoCreateTrajectoryLine = true;

        [Header("Phase 1 — direction wedge (30° sector, sweep, then lock)")]
        [Tooltip("Total angle between the two boundary rays (fanned in front of the player).")]
        [SerializeField] private float sectorAngleDegrees = 30f;
        [SerializeField] private float directionGizmoRadius = 3.5f;
        [Tooltip("Seconds for one full sweep left→right→left along the arc.")]
        [SerializeField] private float directionSweepCycleSeconds = 2.5f;
        [SerializeField] private LineRenderer directionWedgeLine;
        [SerializeField] private LineRenderer directionSweepLine;
        [SerializeField] private bool autoCreateDirectionLines = true;

        [Header("Input (New Input System only)")]
        [Tooltip("Tap when idle = start direction sweep. After locking aim, hold = charge / trajectory, release = cast.")]
        [SerializeField] private InputActionReference castHoldAction;

        [Tooltip("Press once to lock the sweep direction (e.g. Click / South button).")]
        [SerializeField] private InputActionReference lockDirectionAction;

        [Tooltip("Optional: cancel direction sweep or charge (e.g. Escape / East). Leave unset to disable cancel.")]
        [SerializeField] private InputActionReference cancelCastAction;

        [Header("Charge meter (phase 2 — hold to oscillate 0–1, release to cast)")]
        [SerializeField] private float chargeCycleSeconds = 2f;
        [SerializeField] private float minCastDistance = 4f;
        [SerializeField] private float maxCastDistance = 18f;
        [SerializeField] private float launchAngleDegrees = 25f;
        [SerializeField] private float arcHeight = 5f;
        [SerializeField] private float cooldownSeconds = 0.35f;

        [Header("Meter UI (optional)")]
        [SerializeField] private UnityEngine.UI.Slider powerMeterSlider;
        [SerializeField] private UnityEngine.UI.Image powerMeterFillImage;

        [Header("Trajectory Preview")]
        [SerializeField] private int trajectoryPoints = 32;
        [SerializeField] private float trajectoryTimeStep = 0.05f;
        [SerializeField] private LayerMask collisionMask = ~0;

        private float _lastCastTime = -999f;
        private FishingCastPhase _phase = FishingCastPhase.Idle;

        private Vector3 _lockedCastDirection = Vector3.forward;

        private float _directionSweepStartTime;
        private bool _charging;
        private FishingNetProjectile _activeNet;

        public float CurrentCharge01 { get; private set; }

        public FishingCastPhase Phase => _phase;
        public bool HasActiveNet => _activeNet != null;

        private void Reset()
        {
            castOrigin = transform;
            aimTransform = Camera.main != null ? Camera.main.transform : null;
        }

        private void OnEnable()
        {
            castHoldAction?.action?.Enable();
            lockDirectionAction?.action?.Enable();
            cancelCastAction?.action?.Enable();
        }

        private void OnDisable()
        {
            castHoldAction?.action?.Disable();
            lockDirectionAction?.action?.Disable();
            cancelCastAction?.action?.Disable();
        }

        private const string NetPrefabPath = "Assets/Prefabs/Equipment/Net.prefab";

        private void Awake()
        {
            EnsureNetPrefab();
            EnsureTrajectoryLine();
            EnsureDirectionLines();
            HideAllVisuals();
            if (GetComponent<FishingHud>() == null)
                gameObject.AddComponent<FishingHud>();
        }

        private void EnsureNetPrefab()
        {
            if (netPrefab != null) return;

#if UNITY_EDITOR
            var fromAsset = AssetDatabase.LoadAssetAtPath<FishingNetProjectile>(NetPrefabPath);
            if (fromAsset != null)
            {
                netPrefab = fromAsset;
                return;
            }
#endif
            // Fallback: any loaded Net projectile prefab / scene template.
            foreach (var candidate in Resources.FindObjectsOfTypeAll<FishingNetProjectile>())
            {
                if (candidate == null) continue;
                if (candidate.gameObject.scene.IsValid()) continue; // skip scene instances
                netPrefab = candidate;
                return;
            }

            Debug.LogWarning(
                "[Fishing] netPrefab missing. Assign Assets/Prefabs/Equipment/Net on FishingNetCaster.");
        }

        private void HideAllVisuals()
        {
            if (trajectoryLine != null)
            {
                trajectoryLine.enabled = false;
                trajectoryLine.positionCount = 0;
            }

            if (directionWedgeLine != null)
            {
                directionWedgeLine.enabled = false;
                directionWedgeLine.positionCount = 0;
            }

            if (directionSweepLine != null)
            {
                directionSweepLine.enabled = false;
                directionSweepLine.positionCount = 0;
            }

            CurrentCharge01 = 0f;
            HideChargeMeterUi();
        }

        private void EnsureDirectionLines()
        {
            if (!autoCreateDirectionLines) return;

            if (directionWedgeLine == null)
            {
                var go = new GameObject("DirectionWedge");
                go.transform.SetParent(transform, false);
                directionWedgeLine = go.AddComponent<LineRenderer>();
                SetupAuxLine(directionWedgeLine, new Color(1f, 0.85f, 0.2f, 0.9f), 0.05f, 0.05f);
            }

            if (directionSweepLine == null)
            {
                var go = new GameObject("DirectionSweep");
                go.transform.SetParent(transform, false);
                directionSweepLine = go.AddComponent<LineRenderer>();
                SetupAuxLine(directionSweepLine, new Color(0.2f, 1f, 0.45f, 0.95f), 0.07f, 0.04f);
            }
        }

        private static void SetupAuxLine(LineRenderer lr, Color c, float startW, float endW)
        {
            lr.loop = false;
            lr.useWorldSpace = true;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.startWidth = startW;
            lr.endWidth = endW;
            lr.material = Bayou.Rendering.BayouShaderUtil.CreateUnlitColor(c);
        }

        private void EnsureTrajectoryLine()
        {
            if (trajectoryLine != null || !autoCreateTrajectoryLine)
                return;

            var go = new GameObject("TrajectoryPreview");
            go.transform.SetParent(transform, false);
            trajectoryLine = go.AddComponent<LineRenderer>();
            trajectoryLine.loop = false;
            trajectoryLine.useWorldSpace = true;
            trajectoryLine.numCornerVertices = 2;
            trajectoryLine.numCapVertices = 2;
            trajectoryLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trajectoryLine.receiveShadows = false;
            trajectoryLine.startWidth = 0.07f;
            trajectoryLine.endWidth = 0.02f;
            trajectoryLine.material = Bayou.Rendering.BayouShaderUtil.CreateUnlitColor(new Color(0.3f, 0.95f, 1f, 0.85f));
        }

        private void Update()
        {
            switch (_phase)
            {
                case FishingCastPhase.Idle:
                    // Cancel an in-flight / landed net even while caster is idle.
                    if (_activeNet != null)
                    {
                        if (TryCancelFromInput())
                            CancelActiveNet();
                        // Don't start a new cast while a net is still out.
                        break;
                    }

                    if (TryBeginDirectionSweepFromInput())
                    {
                        _phase = FishingCastPhase.DirectionSweep;
                        _directionSweepStartTime = Time.time;
                        ShowDirectionGizmo(true);
                        Bayou.Audio.FishingAudio.Resolve()?.PlayCastConfirm();
                    }
                    break;

                case FishingCastPhase.DirectionSweep:
                    if (TryCancelFromInput())
                    {
                        ResetToIdle();
                        break;
                    }

                    UpdateDirectionSweepVisual();
                    if (TryLockDirectionFromInput())
                    {
                        LockDirectionFromCurrentSweep();
                        _phase = FishingCastPhase.ChargingTrajectory;
                        ShowDirectionGizmo(false);
                        _charging = false;
                        if (trajectoryLine != null)
                        {
                            trajectoryLine.enabled = false;
                            trajectoryLine.positionCount = 0;
                        }
                    }
                    break;

                case FishingCastPhase.ChargingTrajectory:
                    if (TryCancelFromInput())
                    {
                        ResetToIdle();
                        break;
                    }

                    UpdateChargingTrajectoryPhase();
                    break;
            }
        }

        private bool TryBeginDirectionSweepFromInput()
        {
            var a = castHoldAction?.action;
            if (a != null && a.WasPressedThisFrame())
                return true;

            var mouse = Mouse.current;
            return mouse != null && mouse.leftButton.wasPressedThisFrame;
        }

        private bool TryLockDirectionFromInput()
        {
            var a = lockDirectionAction?.action;
            if (a != null && a.WasPressedThisFrame())
                return true;

            // Fallbacks when Lock Direction isn't wired on the prefab:
            // second click, Space, or hold Cast briefly during the sweep.
            var cast = castHoldAction?.action;
            if (cast != null && cast.WasPressedThisFrame())
                return true;

            var kb = Keyboard.current;
            if (kb != null && kb.spaceKey.wasPressedThisFrame)
                return true;

            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
                return true;

            var held = (cast != null && cast.IsPressed()) ||
                       (mouse != null && mouse.leftButton.isPressed);
            return held && (Time.time - _directionSweepStartTime) >= 0.35f;
        }

        private bool TryCancelFromInput()
        {
            var a = cancelCastAction?.action;
            if (a != null && a.WasPressedThisFrame())
                return true;

            var kb = Keyboard.current;
            if (kb != null && (kb.escapeKey.wasPressedThisFrame || kb.qKey.wasPressedThisFrame))
                return true;

            var mouse = Mouse.current;
            return mouse != null && mouse.rightButton.wasPressedThisFrame;
        }

        private void CancelActiveNet()
        {
            if (_activeNet != null)
            {
                _activeNet.CancelAndDestroy();
                _activeNet = null;
            }

            ResetToIdle();
        }

        private void UpdateChargingTrajectoryPhase()
        {
            var pressed = IsCastHeld();

            if (!pressed)
            {
                if (_charging)
                {
                    var releaseCharge = SampleCharge01();
                    _charging = false;
                    TryFireNet(releaseCharge);
                    ResetToIdle();
                }
                // Waiting for hold after aim lock — stay in this phase until hold or cancel.
                return;
            }

            if (!_charging)
            {
                _charging = true;
                _directionSweepStartTime = Time.time;
            }

            CurrentCharge01 = SampleCharge01();
            UpdateChargeMeterUi(CurrentCharge01);
            var (origin, velocity) = ComputeLaunch(CurrentCharge01, _lockedCastDirection);
            DrawTrajectory(origin, velocity);
        }

        private bool IsCastHeld()
        {
            var act = castHoldAction?.action;
            if (act != null && act.IsPressed())
                return true;

            var mouse = Mouse.current;
            return mouse != null && mouse.leftButton.isPressed;
        }

        private void TryFireNet(float charge01)
        {
            if (Time.time - _lastCastTime < cooldownSeconds)
                return;

            _lastCastTime = Time.time;

            EnsureNetPrefab();
            if (netPrefab == null)
            {
                Debug.LogWarning("[Fishing] No netPrefab set on FishingNetCaster. See wiring notes in console.");
                return;
            }

            var (origin, velocity) = ComputeLaunch(charge01, _lockedCastDirection);
            if (_activeNet != null)
                _activeNet.CancelAndDestroy();

            var net = Instantiate(netPrefab, origin, Quaternion.identity);
            _activeNet = net;
            net.Launch(velocity, gameObject);
            Bayou.Audio.FishingAudio.Resolve()?.PlayThrowNet();
        }

        private void ResetToIdle()
        {
            _phase = FishingCastPhase.Idle;
            HideAllVisuals();
            _charging = false;
            CurrentCharge01 = 0f;
        }

        private float SampleCharge01()
        {
            var cycle = Mathf.Max(0.05f, chargeCycleSeconds);
            return Mathf.PingPong((Time.time - _directionSweepStartTime) * (2f / cycle), 1f);
        }

        private void UpdateChargeMeterUi(float charge01)
        {
            if (powerMeterSlider != null)
                powerMeterSlider.normalizedValue = charge01;
            if (powerMeterFillImage != null)
                powerMeterFillImage.fillAmount = charge01;
        }

        private void HideChargeMeterUi()
        {
            UpdateChargeMeterUi(0f);
        }

        private Vector3 GetCenterForwardXZ()
        {
            var aim = aimTransform != null ? aimTransform : transform;
            var fwd = aim.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.001f) fwd = transform.forward;
            fwd.Normalize();
            return fwd;
        }

        private static Vector3 RotateAroundY(Vector3 v, float degrees)
        {
            return Quaternion.AngleAxis(degrees, Vector3.up) * v;
        }

        private float SampleSweepAngleDegrees()
        {
            var half = Mathf.Max(0.5f, sectorAngleDegrees * 0.5f);
            var cycle = Mathf.Max(0.1f, directionSweepCycleSeconds);
            var t = Mathf.PingPong((Time.time - _directionSweepStartTime) * (2f / cycle), 1f);
            return Mathf.Lerp(-half, half, t);
        }

        private void ShowDirectionGizmo(bool show)
        {
            if (directionWedgeLine != null)
                directionWedgeLine.enabled = show;
            if (directionSweepLine != null)
                directionSweepLine.enabled = show;
            if (!show) return;
            UpdateDirectionSweepVisual();
        }

        private void UpdateDirectionSweepVisual()
        {
            var origin = GetLaunchOrigin();
            var center = GetCenterForwardXZ();
            var half = Mathf.Max(0.5f, sectorAngleDegrees * 0.5f);
            var dirA = RotateAroundY(center, -half);
            var dirB = RotateAroundY(center, +half);
            var r = Mathf.Max(0.5f, directionGizmoRadius);
            var ptA = origin + dirA * r;
            var ptB = origin + dirB * r;

            if (directionWedgeLine != null)
            {
                directionWedgeLine.enabled = true;
                directionWedgeLine.positionCount = 4;
                directionWedgeLine.SetPosition(0, origin);
                directionWedgeLine.SetPosition(1, ptA);
                directionWedgeLine.SetPosition(2, origin);
                directionWedgeLine.SetPosition(3, ptB);
            }

            var sweepDeg = SampleSweepAngleDegrees();
            var sweepDir = RotateAroundY(center, sweepDeg).normalized;
            var sweepTip = origin + sweepDir * r;

            if (directionSweepLine != null)
            {
                directionSweepLine.enabled = true;
                directionSweepLine.positionCount = 2;
                directionSweepLine.SetPosition(0, origin);
                directionSweepLine.SetPosition(1, sweepTip);
            }

#if UNITY_EDITOR
            _debugOrigin = origin;
            _debugPtA = ptA;
            _debugPtB = ptB;
            _debugSweepTip = sweepTip;
#endif
        }

        private void LockDirectionFromCurrentSweep()
        {
            var center = GetCenterForwardXZ();
            var sweepDeg = SampleSweepAngleDegrees();
            _lockedCastDirection = RotateAroundY(center, sweepDeg).normalized;
            if (_lockedCastDirection.sqrMagnitude < 0.001f)
                _lockedCastDirection = center;
        }

        private Vector3 GetLaunchOrigin()
        {
            var root = castOrigin != null ? castOrigin.position : transform.position;
            return root + Vector3.up * Mathf.Max(0.5f, castHeightOffset);
        }

        private (Vector3 origin, Vector3 velocity) ComputeLaunch(float charge01, Vector3 flatForward)
        {
            charge01 = Mathf.Clamp01(charge01);

            var origin = GetLaunchOrigin();
            var fwd = flatForward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.001f) fwd = GetCenterForwardXZ();
            fwd.Normalize();

            var distance = Mathf.Lerp(minCastDistance, maxCastDistance, charge01);
            distance = Mathf.Clamp(distance, 0.5f, 999f);

            var target = origin + fwd * distance;
            var angle = Mathf.Clamp(launchAngleDegrees, 5f, 75f) * Mathf.Deg2Rad;

            var g = Mathf.Abs(Physics.gravity.y);
            var height = Mathf.Max(0.1f, arcHeight);
            var tUp = Mathf.Sqrt(2f * height / g);
            var tDown = tUp;
            var t = Mathf.Max(0.2f, tUp + tDown);

            var planar = new Vector3(target.x - origin.x, 0f, target.z - origin.z);
            var vPlanar = planar / t;

            var vY = Mathf.Tan(angle) * vPlanar.magnitude;
            var v = new Vector3(vPlanar.x, vY, vPlanar.z);

            return (origin, v);
        }

        private void DrawTrajectory(Vector3 origin, Vector3 velocity)
        {
            if (trajectoryLine == null) return;

            trajectoryLine.enabled = true;

            var count = Mathf.Clamp(trajectoryPoints, 2, 128);
            trajectoryLine.positionCount = count;

            var grav = Physics.gravity;
            var tStep = Mathf.Max(0.01f, trajectoryTimeStep);

            var pos = origin;
            var vel = velocity;

            trajectoryLine.SetPosition(0, pos);

            for (var i = 1; i < count; i++)
            {
                var nextVel = vel + grav * tStep;
                var nextPos = pos + vel * tStep + 0.5f * grav * (tStep * tStep);

                if (Physics.Linecast(pos, nextPos, out var hit, collisionMask, QueryTriggerInteraction.Ignore))
                {
                    trajectoryLine.SetPosition(i, hit.point);
                    trajectoryLine.positionCount = i + 1;
                    return;
                }

                trajectoryLine.SetPosition(i, nextPos);
                pos = nextPos;
                vel = nextVel;
            }
        }

#if UNITY_EDITOR
        private Vector3 _debugOrigin, _debugPtA, _debugPtB, _debugSweepTip;

        private void OnDrawGizmosSelected()
        {
            if (!isActiveAndEnabled || _phase != FishingCastPhase.DirectionSweep) return;
            Gizmos.color = new Color(1f, 0.8f, 0.1f, 0.9f);
            Gizmos.DrawLine(_debugOrigin, _debugPtA);
            Gizmos.DrawLine(_debugOrigin, _debugPtB);
            Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.95f);
            Gizmos.DrawLine(_debugOrigin, _debugSweepTip);
        }
#endif
    }
}
