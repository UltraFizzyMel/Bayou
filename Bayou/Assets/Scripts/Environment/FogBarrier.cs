using System.Collections.Generic;
using Bayou.Fishing;
using Bayou.Player;
using UnityEngine;

namespace Bayou.Environment
{
    /// <summary>
    /// Fog volume. Staying inside without a lit lantern too long sends the player back out.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FogBarrier : MonoBehaviour
    {
        [SerializeField] private float maxSecondsWithoutLantern = 4.25f;
        [SerializeField] private float warningStartsAt = 1.35f;
        [SerializeField] private float pushBackDistance = 3.2f;
        [SerializeField] private float cooldownAfterPush = 1.1f;
        [SerializeField] private string playerTag = "Player";

        private static readonly HashSet<FogBarrier> Inside = new();
        private static int _tickFrame = -1;
        private static float _unsafeSeconds;
        private static Vector3 _lastSafePos;
        private static bool _hasSafe;
        private static float _nextPushTime;
        private static bool _wasInside;

        /// <summary>0–1 how close the player is to being forced out.</summary>
        public static float Warning01 { get; private set; }

        /// <summary>True while the player is in fog without a lantern.</summary>
        public static bool IsThreatening { get; private set; }

        public static string WarningText =>
            Warning01 <= 0.001f
                ? string.Empty
                : Warning01 >= 0.72f
                    ? "The fog is swallowing you. Light a lantern!"
                    : "The fog thickens. Hold your lantern.";

        private void Reset()
        {
            var col = GetComponent<Collider>();
            if (col != null)
                col.isTrigger = true;
        }

        private void Awake()
        {
            var col = GetComponent<Collider>();
            if (col != null)
                col.isTrigger = true;
        }

        private void OnDisable()
        {
            Inside.Remove(this);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (IsPlayer(other))
                Inside.Add(this);
        }

        private void OnTriggerStay(Collider other)
        {
            if (IsPlayer(other))
                Inside.Add(this);
        }

        private void OnTriggerExit(Collider other)
        {
            if (IsPlayer(other))
                Inside.Remove(this);
        }

        private void Update()
        {
            if (Time.frameCount == _tickFrame)
                return;
            _tickFrame = Time.frameCount;
            Tick();
        }

        private static void Tick()
        {
            Inside.RemoveWhere(b => b == null || !b.isActiveAndEnabled);

            var player = PlayerLocator.Transform;
            if (player == null)
            {
                Warning01 = 0f;
                IsThreatening = false;
                return;
            }

            var inside = Inside.Count > 0;
            var lantern = HeldLantern.IsAnyLit;

            if (!inside)
            {
                _lastSafePos = player.position;
                _hasSafe = true;
                _unsafeSeconds = 0f;
                Warning01 = 0f;
                IsThreatening = false;
                _wasInside = false;
                return;
            }

            if (!_wasInside)
            {
                if (!_hasSafe)
                {
                    var fallback = player.position;
                    foreach (var b in Inside)
                    {
                        if (b == null) continue;
                        var back = -b.transform.forward;
                        back.y = 0f;
                        if (back.sqrMagnitude < 0.01f)
                            back = Vector3.back;
                        fallback = b.transform.position + back.normalized * 4.5f;
                        fallback.y = player.position.y;
                        break;
                    }

                    _lastSafePos = fallback;
                    _hasSafe = true;
                }

                _unsafeSeconds = 0f;
            }

            _wasInside = true;

            if (lantern)
            {
                _unsafeSeconds = Mathf.MoveTowards(_unsafeSeconds, 0f, Time.deltaTime * 1.6f);
                Warning01 = 0f;
                IsThreatening = false;
                return;
            }

            IsThreatening = true;
            _unsafeSeconds += Time.deltaTime;

            var max = 4.25f;
            var warnAt = 1.35f;
            foreach (var barrier in Inside)
            {
                if (barrier == null) continue;
                max = Mathf.Max(0.75f, barrier.maxSecondsWithoutLantern);
                warnAt = Mathf.Max(0.2f, barrier.warningStartsAt);
                break;
            }

            Warning01 = Mathf.InverseLerp(warnAt, max, _unsafeSeconds);

            if (_unsafeSeconds >= max && Time.time >= _nextPushTime)
                PushPlayerOut(player);
        }

        private static void PushPlayerOut(Transform player)
        {
            var barrier = default(FogBarrier);
            foreach (var b in Inside)
            {
                if (b == null) continue;
                barrier = b;
                break;
            }

            var origin = barrier != null ? barrier.transform.position : player.position;
            var safe = _hasSafe ? _lastSafePos : origin;
            var away = safe - origin;
            away.y = 0f;
            if (away.sqrMagnitude < 0.05f)
            {
                var fwd = barrier != null ? barrier.transform.forward : Vector3.back;
                fwd.y = 0f;
                away = fwd.sqrMagnitude > 0.01f ? -fwd.normalized : Vector3.back;
            }
            else
            {
                away.Normalize();
            }

            var push = barrier != null ? barrier.pushBackDistance : 3.2f;
            var dest = safe;
            dest.y = player.position.y;
            if (Vector3.Distance(new Vector3(player.position.x, 0f, player.position.z),
                    new Vector3(dest.x, 0f, dest.z)) < 0.75f)
                dest += away * push;

            var rb = player.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.position = dest;
            }
            else
            {
                player.position = dest;
            }

            _unsafeSeconds = 0f;
            Warning01 = 0f;
            _nextPushTime = Time.time + (barrier != null ? barrier.cooldownAfterPush : 1.1f);
            Debug.Log("[Fog] Sent player back — lantern required to stay in the fog.");
        }

        private bool IsPlayer(Collider other) =>
            other != null &&
            (other.CompareTag(playerTag) ||
             other.GetComponentInParent<BayouCharacterMotor>() != null);
    }
}
