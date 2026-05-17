using UnityEngine;

namespace Bayou.Fish
{
    /// <summary>
    /// Simple surface fish: wanders on XZ, flees from the player, can be caught by hand net overlap.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BayouFish : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float wanderSpeed = 1.1f;
        [SerializeField] private float wanderIntervalMin = 1.2f;
        [SerializeField] private float wanderIntervalMax = 3.5f;
        [SerializeField] private float fleeSpeed = 2.8f;
        [SerializeField] private float fleeRadius = 3.5f;

        [Header("References")]
        [SerializeField] private Transform player;

        public bool IsCaught { get; private set; }

        private Vector3 _wanderDir;
        private float _nextWanderTime;

        private void Awake()
        {
            if (player == null)
            {
                var p = GameObject.FindGameObjectWithTag("Player");
                if (p != null) player = p.transform;
            }

            PickNewWanderDir();
        }

        private void Update()
        {
            if (IsCaught) return;

            var dt = Time.deltaTime;
            var pos = transform.position;

            if (player != null)
            {
                var toPlayer = Flat(pos - player.position);
                if (toPlayer.magnitude < fleeRadius && toPlayer.sqrMagnitude > 0.0001f)
                {
                    transform.position += (-toPlayer.normalized) * (fleeSpeed * dt);
                    return;
                }
            }

            if (Time.time >= _nextWanderTime)
                PickNewWanderDir();

            transform.position += _wanderDir * (wanderSpeed * dt);
        }

        /// <summary>Called by <see cref="Fishing.HandNetAreaController"/> when the net is used.</summary>
        public void TryCatchFromNet(Vector3 netCenter, float radius)
        {
            if (IsCaught) return;

            var p = Flat(transform.position - netCenter);
            if (p.magnitude <= radius)
                Catch();
        }

        public void Catch()
        {
            if (IsCaught) return;
            IsCaught = true;
            _wanderDir = Vector3.zero;
        }

        private void PickNewWanderDir()
        {
            var a = Random.Range(0f, Mathf.PI * 2f);
            _wanderDir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)).normalized;
            _nextWanderTime = Time.time + Random.Range(wanderIntervalMin, wanderIntervalMax);
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }
    }
}
