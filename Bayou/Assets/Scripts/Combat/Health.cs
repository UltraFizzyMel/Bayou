using System;
using UnityEngine;
using UnityEngine.Events;

namespace Bayou.Combat
{
    [DisallowMultipleComponent]
    public sealed class Health : MonoBehaviour
    {
        [SerializeField] private float maxHealth = 50f;
        [SerializeField] private CombatTeam team = CombatTeam.Neutral;
        [SerializeField] private bool destroyOnDeath;
        [SerializeField] private float destroyDelay = 0.35f;

        [Header("Events")]
        [SerializeField] private UnityEvent onDamaged;
        [SerializeField] private UnityEvent onDeath;

        public event Action<float, GameObject> Damaged;
        public event Action Died;

        public float MaxHealth => maxHealth;
        public float Current { get; private set; }
        public bool IsDead { get; private set; }
        public CombatTeam Team => team;

        private void Awake()
        {
            Current = Mathf.Max(1f, maxHealth);
        }

        public void SetTeam(CombatTeam value) => team = value;

        public bool CanBeDamagedBy(CombatTeam attackerTeam)
        {
            if (IsDead) return false;
            if (attackerTeam == CombatTeam.Neutral || team == CombatTeam.Neutral)
                return true;
            return attackerTeam != team;
        }

        public bool ApplyDamage(float amount, GameObject source, CombatTeam attackerTeam)
        {
            if (!CanBeDamagedBy(attackerTeam)) return false;
            if (amount <= 0f) return false;

            Current = Mathf.Max(0f, Current - amount);
            onDamaged?.Invoke();
            Damaged?.Invoke(amount, source);

            if (Current <= 0f && !IsDead)
                Die();

            return true;
        }

        public void Heal(float amount)
        {
            if (IsDead || amount <= 0f) return;
            Current = Mathf.Min(maxHealth, Current + amount);
        }

        private void Die()
        {
            IsDead = true;
            onDeath?.Invoke();
            Died?.Invoke();

            if (destroyOnDeath)
                Destroy(gameObject, Mathf.Max(0f, destroyDelay));
        }
    }
}
