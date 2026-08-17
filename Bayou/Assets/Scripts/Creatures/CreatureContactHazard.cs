using UnityEngine;

namespace Bayou.Creatures
{
    /// <summary>
    /// While Active, overlapping the player applies knockback / hurt callbacks.
    /// Wire an optional <see cref="IPlayerHurtReceiver"/> on the player for HP later.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CreatureContactHazard : MonoBehaviour
    {
        [SerializeField] private float knockbackSpeed = 6f;
        [SerializeField] private float hitCooldownSeconds = 1.1f;
        [SerializeField] private float damage = 1f;
        [SerializeField] private string playerTag = "Player";

        private float _nextHitTime;
        private CreatureController _owner;

        private void Awake()
        {
            _owner = GetComponent<CreatureController>();
        }

        private void OnTriggerStay(Collider other)
        {
            TryHit(other);
        }

        private void OnCollisionStay(Collision collision)
        {
            if (collision != null)
                TryHit(collision.collider);
        }

        private void TryHit(Collider other)
        {
            if (other == null) return;
            if (_owner != null && (_owner.IsCaught || _owner.IsStunned || !_owner.IsActive))
                return;
            if (Time.time < _nextHitTime) return;
            if (!IsPlayer(other)) return;

            var player = other.attachedRigidbody != null
                ? other.attachedRigidbody.transform
                : other.transform.root;

            var away = player.position - transform.position;
            away.y = 0f;
            if (away.sqrMagnitude < 0.001f)
                away = transform.forward;
            away.Normalize();

            var receiver = player.GetComponentInChildren<IPlayerHurtReceiver>();
            if (receiver != null)
                receiver.OnCreatureHit(new CreatureHitInfo(damage, away * knockbackSpeed, gameObject));
            else
            {
                // Fallback: shove CharacterController / Rigidbody if present.
                var cc = player.GetComponent<CharacterController>();
                if (cc != null && cc.enabled)
                    cc.Move(away * knockbackSpeed * Time.deltaTime * 8f);
                else
                {
                    var rb = player.GetComponent<Rigidbody>();
                    if (rb != null && !rb.isKinematic)
                        rb.AddForce(away * knockbackSpeed, ForceMode.VelocityChange);
                }
            }

            _nextHitTime = Time.time + hitCooldownSeconds;
        }

        private bool IsPlayer(Collider other) =>
            other != null &&
            (other.CompareTag(playerTag) ||
             other.GetComponentInParent<Bayou.Player.BayouCharacterMotor>() != null);
    }

    public readonly struct CreatureHitInfo
    {
        public readonly float Damage;
        public readonly Vector3 KnockbackVelocity;
        public readonly GameObject Source;

        public CreatureHitInfo(float damage, Vector3 knockbackVelocity, GameObject source)
        {
            Damage = damage;
            KnockbackVelocity = knockbackVelocity;
            Source = source;
        }
    }

    public interface IPlayerHurtReceiver
    {
        void OnCreatureHit(CreatureHitInfo info);
    }
}
