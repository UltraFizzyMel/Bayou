using System.Collections;
using System.Collections.Generic;
using Bayou.Combat;
using Bayou.Creatures;
using Bayou.Rendering;
using UnityEngine;

namespace Bayou.Fishing
{
    /// <summary>
    /// Timed arc swing: a trail-tipped blade sweeps in front of the player and hits
    /// each creature once as the net/rod passes through them.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MeleeSweepAttack : MonoBehaviour
    {
        [SerializeField] private float swingDuration = 0.28f;
        [SerializeField] private float bladeWidthDegrees = 58f;
        [SerializeField] private float trailTime = 0.32f;
        [SerializeField] private Color netTrailColor = new(0.55f, 0.95f, 1f, 0.95f);
        [SerializeField] private Color rodTrailColor = new(0.95f, 0.72f, 0.32f, 0.95f);

        public bool IsSwinging => _swinging;

        private Transform _tip;
        private TrailRenderer _trail;
        private LineRenderer _blade;
        private Coroutine _routine;
        private bool _swinging;
        private float _currentYaw;
        private Vector3 _lockedForward = Vector3.forward;
        private readonly List<CreatureController> _sliceHits = new(8);
        private Transform _heldProp;
        private Quaternion _heldPropBindRot;
        private Vector3 _heldPropBindPos;

        public float CurrentYawDegrees => _currentYaw;
        public Vector3 LockedForward => _lockedForward;

        public bool TryPlay(
            NetHitSource source,
            float range,
            float arcDegrees,
            float guaranteedRadius,
            Color? trailOverride = null)
        {
            if (_swinging) return false;
            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(Sweep(source, range, arcDegrees, guaranteedRadius, trailOverride));
            return true;
        }

        private IEnumerator Sweep(
            NetHitSource source,
            float range,
            float arcDegrees,
            float guaranteedRadius,
            Color? trailOverride)
        {
            _swinging = true;
            EnsureVfx();

            var origin = transform.position + Vector3.up * 0.35f;
            _lockedForward = Bayou.Player.BayouFacing.GetCardinalForward8(transform);
            var half = Mathf.Clamp(arcDegrees, 40f, 180f) * 0.5f;
            var duration = Mathf.Max(0.12f, swingDuration);
            var color = trailOverride ?? (source == NetHitSource.MeleeRod ? rodTrailColor : netTrailColor);

            ToolMelee.BeginSweep();
            BeginTrail(color);
            Bayou.Audio.FishingAudio.Resolve()?.PlayMeleeSwing();
            CacheHeldProp(source);

            var hitAny = false;
            var t = 0f;
            while (t < duration)
            {
                var u = Mathf.Clamp01(t / duration);
                var eased = u * u * (3f - 2f * u);
                _currentYaw = Mathf.Lerp(-half, half, eased);
                origin = transform.position + Vector3.up * 0.35f;
                UpdateVfx(origin, range, color);
                PoseHeldProp(eased);

                _sliceHits.Clear();
                if (ToolMelee.TryHitSweepSlice(
                        origin,
                        _lockedForward,
                        range,
                        _currentYaw,
                        bladeWidthDegrees,
                        guaranteedRadius,
                        source,
                        _sliceHits))
                {
                    if (!hitAny)
                        Bayou.Audio.FishingAudio.Resolve()?.PlayMeleeHit();
                    hitAny = true;
                }

                t += Time.deltaTime;
                yield return null;
            }

            _currentYaw = half;
            UpdateVfx(origin, range, color);
            _sliceHits.Clear();
            if (ToolMelee.TryHitSweepSlice(
                    origin, _lockedForward, range, _currentYaw, bladeWidthDegrees,
                    guaranteedRadius, source, _sliceHits))
            {
                if (!hitAny)
                    Bayou.Audio.FishingAudio.Resolve()?.PlayMeleeHit();
                hitAny = true;
            }

            if (!hitAny)
                CombatFeedback.PlayWhiff(origin, _lockedForward);

            RestoreHeldProp();
            EndTrail();
            _swinging = false;
            _routine = null;
        }

        private void EnsureVfx()
        {
            if (_tip == null)
            {
                var go = new GameObject("MeleeSweepTip");
                go.transform.SetParent(transform, false);
                _tip = go.transform;
                _trail = go.AddComponent<TrailRenderer>();
                _trail.time = trailTime;
                _trail.minVertexDistance = 0.02f;
                _trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _trail.receiveShadows = false;
                _trail.widthCurve = AnimationCurve.EaseInOut(0f, 0.22f, 1f, 0.02f);
                _trail.emitting = false;
                _trail.numCapVertices = 4;
                _trail.numCornerVertices = 4;
            }

            if (_blade == null)
            {
                var go = new GameObject("MeleeSweepBlade");
                go.transform.SetParent(transform, false);
                _blade = go.AddComponent<LineRenderer>();
                _blade.loop = false;
                _blade.useWorldSpace = true;
                _blade.positionCount = 2;
                _blade.startWidth = 0.08f;
                _blade.endWidth = 0.18f;
                _blade.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _blade.receiveShadows = false;
                _blade.enabled = false;
            }
        }

        private void BeginTrail(Color color)
        {
            if (_trail != null)
            {
                _trail.material = BayouShaderUtil.CreateUnlitColor(color);
                _trail.startColor = color;
                _trail.endColor = new Color(color.r, color.g, color.b, 0f);
                _trail.Clear();
                _trail.emitting = true;
            }

            if (_blade != null)
            {
                _blade.material = BayouShaderUtil.CreateUnlitColor(color);
                _blade.startColor = new Color(color.r, color.g, color.b, 0.35f);
                _blade.endColor = color;
                _blade.enabled = true;
            }
        }

        private void UpdateVfx(Vector3 origin, float range, Color color)
        {
            var dir = Quaternion.AngleAxis(_currentYaw, Vector3.up) * _lockedForward;
            var tip = origin + dir * (range * 0.92f) + Vector3.up * 0.55f;
            if (_tip != null)
                _tip.position = tip;

            if (_blade != null && _blade.enabled)
            {
                var grip = origin + dir * 0.35f + Vector3.up * 0.2f;
                _blade.SetPosition(0, grip);
                _blade.SetPosition(1, tip);
                var pulse = 0.55f + 0.45f * Mathf.Sin(Time.time * 28f);
                var c = color;
                c.a = pulse;
                _blade.endColor = c;
            }
        }

        private void EndTrail()
        {
            if (_trail != null)
                _trail.emitting = false;
            if (_blade != null)
                _blade.enabled = false;
        }

        private void CacheHeldProp(NetHitSource source)
        {
            _heldProp = FindHeldProp(source);
            if (_heldProp == null) return;
            _heldPropBindRot = _heldProp.localRotation;
            _heldPropBindPos = _heldProp.localPosition;
        }

        private void PoseHeldProp(float eased01)
        {
            if (_heldProp == null) return;
            var yaw = Mathf.Lerp(70f, -80f, eased01);
            var pitch = Mathf.Lerp(10f, 55f, Mathf.Sin(eased01 * Mathf.PI));
            _heldProp.localRotation = _heldPropBindRot * Quaternion.Euler(pitch, yaw, 0f);
            var reach = Mathf.Sin(eased01 * Mathf.PI) * 0.18f;
            _heldProp.localPosition = _heldPropBindPos + transform.InverseTransformDirection(_lockedForward) * reach;
        }

        private void RestoreHeldProp()
        {
            if (_heldProp == null) return;
            _heldProp.localRotation = _heldPropBindRot;
            _heldProp.localPosition = _heldPropBindPos;
            _heldProp = null;
        }

        private Transform FindHeldProp(NetHitSource source)
        {
            var wantNet = source != NetHitSource.MeleeRod;
            var all = GetComponentsInChildren<Transform>(true);
            Transform fallback = null;
            for (var i = 0; i < all.Length; i++)
            {
                var t = all[i];
                if (t == null || t == transform) continue;
                var n = t.name;
                var isNet = n.IndexOf("HeldNet", System.StringComparison.OrdinalIgnoreCase) >= 0;
                var isRod = n.IndexOf("HeldRod", System.StringComparison.OrdinalIgnoreCase) >= 0;
                if (!isNet && !isRod) continue;
                if ((wantNet && isNet) || (!wantNet && isRod))
                {
                    if (t.gameObject.activeInHierarchy)
                        return t;
                    fallback = t;
                }
            }

            return fallback;
        }

        private void OnDisable()
        {
            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }

            RestoreHeldProp();
            EndTrail();
            _swinging = false;
        }
    }
}
