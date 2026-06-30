using System;
using UnityEngine;

namespace Bayou.Inventory
{
    [DisallowMultipleComponent]
    public sealed class PlayerWallet : MonoBehaviour
    {
        public static PlayerWallet Instance { get; private set; }

        [SerializeField] private int startingMoney = 500;

        public int Balance { get; private set; }
        public event Action BalanceChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[Wallet] Multiple PlayerWallet instances; replacing singleton.");
            }

            Instance = this;
            Balance = Mathf.Max(0, startingMoney);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public bool CanAfford(int amount) => amount <= 0 || Balance >= amount;

        public bool TrySpend(int amount)
        {
            if (amount <= 0) return true;
            if (Balance < amount) return false;
            Balance -= amount;
            BalanceChanged?.Invoke();
            return true;
        }

        public void Add(int amount)
        {
            if (amount <= 0) return;
            Balance += amount;
            BalanceChanged?.Invoke();
        }

        public void SetBalance(int amount)
        {
            Balance = Mathf.Max(0, amount);
            BalanceChanged?.Invoke();
        }
    }
}
