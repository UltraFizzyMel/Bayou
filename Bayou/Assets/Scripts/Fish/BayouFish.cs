using Bayou.Fishing;
using Bayou.Inventory;
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

        [Header("Catch rules")]
        [SerializeField] private FishCatchTool requiredTool = FishCatchTool.Net;
        [SerializeField] private bool isStatic;

        [Header("References")]
        [SerializeField] private Transform player;

        [Header("Inventory")]
        [SerializeField] private ItemDefinition inventoryItemWhenCaught;

        [Header("Net attraction")]
        [SerializeField] private float attractSwimSpeed = 2.4f;

        public bool IsCaught { get; private set; }
        public FishCatchTool RequiredTool => requiredTool;
        public bool IsStatic => isStatic;
        public FishingSpot HomeSpot { get; private set; }
        public ItemDefinition InventoryItem => inventoryItemWhenCaught;

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
                var p = GameObject.FindGameObjectWithTag("Player");
                if (p != null)
                    player = p.transform;
            }

            _wobbleSeed = Random.Range(0f, 1000f);
            PickNewDirection();
            _currentDirection = _targetDirection;
        }

        public void Configure(ItemDefinition item, FishCatchTool tool, FishingSpot home, bool moving)
        {
            inventoryItemWhenCaught = item;
            requiredTool = tool;
            HomeSpot = home;
            isStatic = !moving;
            if (home != null)
            {
                roamRadius = Mathf.Min(roamRadius, home.Radius * 0.55f);
                _spawnPosition = home.ClampInside(transform.position);
                transform.position = _spawnPosition;
            }

            if (isStatic)
            {
                wanderSpeed = 0f;
                fleeSpeed = 0f;
            }
        }

        public bool CanCatchWith(FishCatchTool tool) => requiredTool == tool;

        public void SetAttractTarget(Vector3 worldPoint, float pull01)
        {
            if (isStatic || !CanCatchWith(FishCatchTool.Rod)) return;
            _hasAttractTarget = true;
            _attractTarget = HomeSpot != null ? HomeSpot.ClampInside(worldPoint) : worldPoint;
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

            if (isStatic)
            {
                KeepInsideWater();
                return;
            }

            var dt = Time.deltaTime;
            SteerAwayFromShore();

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

            if (player != null)
            {
                var away = Flat(transform.position - player.position);
                if (away.magnitude < fleeRadius)
                {
                    // Prefer fleeing along shore rather than onto land.
                    var fleeDir = away.normalized;
                    if (HomeSpot != null && HomeSpot.IsNearShore(transform.position + fleeDir))
                    {
                        var toCenter = Flat(HomeSpot.SwimCenter - transform.position);
                        if (toCenter.sqrMagnitude > 0.0001f)
                            fleeDir = (fleeDir + toCenter.normalized).normalized;
                    }

                    _currentDirection = Vector3.RotateTowards(
                        _currentDirection, fleeDir, Mathf.Deg2Rad * turnSpeed * 3f * dt, 0f);
                    Move(fleeSpeed, dt);
                    return;
                }
            }

            if (Time.time >= _nextDirectionChange)
                PickNewDirection();

            _currentDirection = Vector3.RotateTowards(
                _currentDirection, _targetDirection, Mathf.Deg2Rad * turnSpeed * dt, 0f);

            var wobble = Mathf.Sin((Time.time + _wobbleSeed) * 2f) * swimWobble;
            _currentDirection = Quaternion.Euler(0f, wobble * dt, 0f) * _currentDirection;

            var home = HomeSpot != null ? HomeSpot.SwimCenter : _spawnPosition;
            var toHome = home - transform.position;
            toHome.y = 0f;
            if (toHome.magnitude > roamRadius * 0.75f)
                _targetDirection = toHome.normalized;

            Move(wanderSpeed, dt);
        }

        private void Move(float speed, float dt)
        {
            transform.position += _currentDirection.normalized * speed * dt;
            KeepInsideWater();

            if (_currentDirection.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    Quaternion.LookRotation(_currentDirection),
                    dt * 5f);
            }
        }

        private void KeepInsideWater()
        {
            if (HomeSpot == null) return;
            var before = transform.position;
            var clamped = HomeSpot.ClampInside(before);
            if ((clamped - before).sqrMagnitude > 0.00001f)
            {
                transform.position = clamped;
                var inward = Flat(HomeSpot.SwimCenter - clamped);
                if (inward.sqrMagnitude > 0.0001f)
                {
                    _currentDirection = inward.normalized;
                    _targetDirection = _currentDirection;
                }
            }
        }

        private void SteerAwayFromShore()
        {
            if (HomeSpot == null || !HomeSpot.IsNearShore(transform.position)) return;
            var inward = Flat(HomeSpot.SwimCenter - transform.position);
            if (inward.sqrMagnitude < 0.0001f) return;
            _targetDirection = inward.normalized;
        }

        private void PickNewDirection()
        {
            var randomAngle = Random.Range(-randomTurnStrength, randomTurnStrength);
            _targetDirection = Quaternion.Euler(0f, randomAngle, 0f) *
                               (_currentDirection == Vector3.zero ? Random.insideUnitSphere : _currentDirection);
            _targetDirection.y = 0f;
            _targetDirection.Normalize();

            // Bias new headings toward open water when near the edge.
            if (HomeSpot != null && HomeSpot.IsNearShore(transform.position, 0.7f))
            {
                var inward = Flat(HomeSpot.SwimCenter - transform.position);
                if (inward.sqrMagnitude > 0.0001f)
                    _targetDirection = (inward.normalized + _targetDirection * 0.35f).normalized;
            }

            _nextDirectionChange = Time.time + Random.Range(
                directionChangeInterval * 0.6f, directionChangeInterval * 1.4f);
        }

        public void TryCatchFromNet(Vector3 netCenter, float radius)
        {
            if (IsCaught || !CanCatchWith(FishCatchTool.Net))
                return;

            if (HomeSpot != null && !HomeSpot.Contains(netCenter) && !HomeSpot.Contains(transform.position))
                return;

            if (FishingSpot.AnySpotsExist() && FishingSpot.FindContaining(netCenter) == null &&
                FishingSpot.FindContaining(transform.position) == null)
                return;

            if (Vector3.Distance(Flat(transform.position), Flat(netCenter)) <= radius)
                Catch();
        }

        public void Catch()
        {
            if (IsCaught) return;
            IsCaught = true;
            Bayou.Audio.FishingAudio.Resolve()?.PlaySnagCatch();
            gameObject.SetActive(false);

            if (inventoryItemWhenCaught == null)
            {
                Debug.LogWarning($"[Fish] {name} has no inventoryItemWhenCaught assigned.");
                return;
            }

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
            Gizmos.DrawWireSphere(Application.isPlaying ? _spawnPosition : transform.position, roamRadius);
        }
    }
}
