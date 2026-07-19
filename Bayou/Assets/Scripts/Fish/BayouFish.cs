using Bayou.Inventory;
using Bayou.Fishing;
using UnityEngine;

namespace Bayou.Fish
{
    [DisallowMultipleComponent]
    public sealed class BayouFish : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float wanderSpeed = 1.2f;
        [SerializeField] private float turnSpeed = 60f;
        [SerializeField] private float directionChangeInterval = 2f;
        [SerializeField] private float randomTurnStrength = 35f;
        [SerializeField] private float swimWobble = 20f;
        [SerializeField] private float fleeSpeed = 3f;
        [SerializeField] private float fleeRadius = 3.5f;
        [SerializeField] private float roamRadius = 5f;

        [Header("References")]
        [SerializeField] private Transform player;

        [Header("Inventory")]
        [SerializeField] private ItemDefinition inventoryItemWhenCaught;

        [Header("Net attraction")]
        [SerializeField] private float attractSwimSpeed = 2.4f;

        public bool IsCaught { get; private set; }

        private Vector3 _spawnPosition;
        private Vector3 _currentDirection;
        private Vector3 _targetDirection;

        private float _nextDirectionChange;
        private float _wobbleSeed;

        private bool _hasAttractTarget;
        private Vector3 _attractTarget;
        private float _attractPull01;

        private void Awake()
        {
            _spawnPosition = transform.position;

            if (player == null)
            {
                GameObject p = GameObject.FindGameObjectWithTag("Player");
                if (p != null)
                    player = p.transform;
            }

            _wobbleSeed = Random.Range(0f, 1000f);

            PickNewDirection();
            _currentDirection = _targetDirection;
        }

        public void SetAttractTarget(Vector3 worldPoint, float pull01)
        {
            _hasAttractTarget = true;
            _attractTarget = worldPoint;
            _attractPull01 = Mathf.Clamp01(pull01);
        }

        public void ClearAttractTarget()
        {
            _hasAttractTarget = false;
            _attractPull01 = 0f;
        }

        private void Update()
        {
            if (IsCaught)
                return;

            float dt = Time.deltaTime;

            // Swim toward planted net when attracted (wiggle strengthens pull).
            if (_hasAttractTarget)
            {
                var toNet = Flat(_attractTarget - transform.position);
                if (toNet.sqrMagnitude > 0.0001f)
                {
                    _targetDirection = toNet.normalized;
                    _currentDirection = Vector3.RotateTowards(
                        _currentDirection,
                        _targetDirection,
                        Mathf.Deg2Rad * turnSpeed * (1.5f + _attractPull01 * 2f) * dt,
                        0f);

                    var speed = Mathf.Lerp(wanderSpeed, attractSwimSpeed, _attractPull01);
                    Move(speed, dt);
                    return;
                }
            }

            // Flee player
            if (player != null)
            {
                Vector3 away = Flat(transform.position - player.position);

                if (away.magnitude < fleeRadius)
                {
                    away.Normalize();

                    _currentDirection = Vector3.RotateTowards(
                        _currentDirection,
                        away,
                        Mathf.Deg2Rad * turnSpeed * 3f * dt,
                        0f);

                    Move(fleeSpeed, dt);
                    return;
                }
            }

            if (Time.time >= _nextDirectionChange)
                PickNewDirection();

            // Smooth steering
            _currentDirection = Vector3.RotateTowards(
                _currentDirection,
                _targetDirection,
                Mathf.Deg2Rad * turnSpeed * dt,
                0f);

            // Small continuous wobble
            float wobble =
                Mathf.Sin((Time.time + _wobbleSeed) * 2f) *
                swimWobble;

            Quaternion wobbleRot =
                Quaternion.Euler(0f, wobble * dt, 0f);

            _currentDirection = wobbleRot * _currentDirection;

            // Keep fish inside roam radius
            Vector3 toHome = _spawnPosition - transform.position;

            if (toHome.magnitude > roamRadius * 0.8f)
            {
                _targetDirection = toHome.normalized;
            }

            Move(wanderSpeed, dt);
        }

        private void Move(float speed, float dt)
        {
            transform.position += _currentDirection.normalized * speed * dt;

            if (_currentDirection.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation =
                    Quaternion.LookRotation(_currentDirection);

                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    targetRotation,
                    dt * 5f);
            }
        }

        private void PickNewDirection()
        {
            float randomAngle = Random.Range(
                -randomTurnStrength,
                randomTurnStrength);

            Quaternion rotation =
                Quaternion.Euler(0f, randomAngle, 0f);

            _targetDirection =
                rotation * (_currentDirection == Vector3.zero
                    ? Random.insideUnitSphere
                    : _currentDirection);

            _targetDirection.y = 0f;
            _targetDirection.Normalize();

            _nextDirectionChange =
                Time.time + Random.Range(
                    directionChangeInterval * 0.6f,
                    directionChangeInterval * 1.4f);
        }

        public void TryCatchFromNet(Vector3 netCenter, float radius)
        {
            if (IsCaught)
                return;

            if (!FishingZoneManager.IsInFishingZone(transform.position))
                return;

            if (Vector3.Distance(
                Flat(transform.position),
                Flat(netCenter)) <= radius)
            {
                Catch();
            }
        }

        public void Catch()
        {
            if (IsCaught)
                return;

            IsCaught = true;

            Bayou.Audio.FishingAudio.Resolve()?.PlaySnagCatch();

            gameObject.SetActive(false);

            if (inventoryItemWhenCaught == null)
            {
                Debug.LogWarning($"[Fish] {name} has no inventoryItemWhenCaught assigned.");
                return;
            }

            // Reveal → open bag → player places or discards (no auto-slot).
            CaughtFishPresenter.Present(inventoryItemWhenCaught);
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(
                Application.isPlaying
                    ? _spawnPosition
                    : transform.position,
                roamRadius);
        }
    }
}