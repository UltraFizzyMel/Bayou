using Bayou.Fishing;
using UnityEngine;

namespace Bayou.Player
{
    /// <summary>Cached player refs so gameplay code never searches the scene each frame.</summary>
    public static class PlayerLocator
    {
        private static Transform _transform;
        private static BayouCharacterMotor _motor;
        private static BayouFishingEquipment _equipment;
        private static float _nextRetry;

        public static Transform Transform
        {
            get
            {
                if (_transform != null) return _transform;
                TryResolve();
                return _transform;
            }
        }

        public static BayouCharacterMotor Motor
        {
            get
            {
                if (_motor != null) return _motor;
                TryResolve();
                return _motor;
            }
        }

        public static BayouFishingEquipment Equipment
        {
            get
            {
                if (_equipment != null) return _equipment;
                TryResolve();
                return _equipment;
            }
        }

        public static void Bind(GameObject player)
        {
            if (player == null) return;
            _transform = player.transform;
            _motor = player.GetComponent<BayouCharacterMotor>();
            _equipment = player.GetComponent<BayouFishingEquipment>();
            _nextRetry = 0f;
        }

        public static void ClearIf(Component owner)
        {
            if (owner == null) return;
            if (_motor == owner || _equipment == owner ||
                (_transform != null && _transform == owner.transform))
                Clear();
        }

        public static void Clear()
        {
            _transform = null;
            _motor = null;
            _equipment = null;
            _nextRetry = 0f;
        }

        private static void TryResolve()
        {
            if (Time.unscaledTime < _nextRetry) return;
            _nextRetry = Time.unscaledTime + 0.5f;

            var go = GameObject.FindGameObjectWithTag("Player");
            if (go != null)
            {
                Bind(go);
                return;
            }

            var motor = Object.FindFirstObjectByType<BayouCharacterMotor>();
            if (motor != null)
                Bind(motor.gameObject);
        }
    }
}
