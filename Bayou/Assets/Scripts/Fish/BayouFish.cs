using System.Collections.Generic;
using Bayou.Fishing;
using Bayou.Inventory;
using Bayou.Player;
using UnityEngine;

namespace Bayou.Fish
{
    [DisallowMultipleComponent]
    public sealed class BayouFish : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float wanderSpeed = 2.35f;
        [SerializeField] private float dartSpeed = 3.6f;
        [SerializeField] private float turnSpeed = 140f;
        [SerializeField] private float fleeSpeed = 4.1f;
        [SerializeField] private float fleeRadius = 3.5f;
        [SerializeField] private float roamRadius = 8f;
        [SerializeField] private float bobAmplitude = 0.055f;
        [SerializeField] private float bobFrequency = 1.35f;
        [SerializeField] private float bankDegrees = 16f;
        [SerializeField] private float neighborSeparation = 2.6f;

        [Header("Catch rules")]
        [SerializeField] private FishCatchTool requiredTool = FishCatchTool.Net;
        [SerializeField] private bool isStatic;

        [Header("References")]
        [SerializeField] private Transform player;

        [Header("Inventory")]
        [SerializeField] private ItemDefinition inventoryItemWhenCaught;

        [Header("Net attraction")]
        [SerializeField] private float attractSwimSpeed = 2.4f;

        private static readonly List<BayouFish> All = new();

        public bool IsCaught { get; private set; }
        public FishCatchTool RequiredTool => requiredTool;
        public bool IsStatic => isStatic;
        public FishingSpot HomeSpot { get; private set; }
        public ItemDefinition InventoryItem => inventoryItemWhenCaught;
        public static IReadOnlyList<BayouFish> Living => All;

        private Vector3 _spawnPosition;
        private Vector3 _currentDirection;
        private Vector3 _targetDirection;
        private Vector3 _swimTarget;
        private bool _hasSwimTarget;
        private float _speed;
        private float _yaw;
        private float _wobbleSeed;
        private float _idleUntil;
        private float _retargetAt;
        private bool _hasAttractTarget;
        private Vector3 _attractTarget;
        private float _attractPull01;

        private void Awake()
        {
            _spawnPosition = transform.position;

            if (player == null)
                player = PlayerLocator.Transform;

            _wobbleSeed = Random.Range(0f, 1000f);
            _speed = wanderSpeed;
            PickRandomSwimTarget();
            _currentDirection = _targetDirection;
            if (_currentDirection.sqrMagnitude < 0.0001f)
                _currentDirection = Vector3.forward;
            _yaw = Mathf.Atan2(_currentDirection.x, _currentDirection.z) * Mathf.Rad2Deg;
        }

        private void OnEnable()
        {
            if (!All.Contains(this))
                All.Add(this);
        }

        private void OnDisable()
        {
            All.Remove(this);
        }

        public void Configure(ItemDefinition item, FishCatchTool tool, FishingSpot home, bool moving)
        {
            inventoryItemWhenCaught = item;
            requiredTool = tool;
            HomeSpot = home;
            isStatic = !moving;
            if (home != null)
            {
                roamRadius = Mathf.Max(1.2f, home.Radius * 0.5f);
                neighborSeparation = Mathf.Clamp(neighborSeparation, 0.8f, Mathf.Max(0.9f, home.Radius * 0.35f));
                _spawnPosition = home.ClampInside(transform.position);
                transform.position = _spawnPosition;
            }

            if (isStatic)
            {
                wanderSpeed = 0f;
                fleeSpeed = 0f;
                dartSpeed = 0f;
            }

            PickRandomSwimTarget();
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
            _hasSwimTarget = false;
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
            if (player == null)
                player = PlayerLocator.Transform;

            var cruise = wanderSpeed * (0.82f + 0.45f * Mathf.PerlinNoise(_wobbleSeed, Time.time * 0.55f));
            var desiredSpeed = cruise;

            if (_hasAttractTarget)
            {
                var toNet = Flat(_attractTarget - transform.position);
                if (toNet.sqrMagnitude > 0.0001f)
                    _targetDirection = toNet.normalized;
                desiredSpeed = Mathf.Lerp(cruise, attractSwimSpeed, _attractPull01);
            }
            else if (player != null)
            {
                var away = Flat(transform.position - player.position);
                if (away.magnitude < fleeRadius)
                {
                    var fleeDir = away.normalized;
                    if (HomeSpot != null && HomeSpot.IsNearShore(transform.position + fleeDir))
                    {
                        var toCenter = Flat(HomeSpot.SwimCenter - transform.position);
                        if (toCenter.sqrMagnitude > 0.0001f)
                            fleeDir = (fleeDir + toCenter.normalized).normalized;
                    }

                    _targetDirection = fleeDir;
                    desiredSpeed = fleeSpeed;
                    _idleUntil = 0f;
                    _hasSwimTarget = false;
                }
                else
                {
                    TickRandomSwim();
                }
            }
            else
            {
                TickRandomSwim();
            }

            SeparateFromNeighbors();

            var turn = turnSpeed * (_hasAttractTarget ? 1.6f + _attractPull01 : 1f);
            if (desiredSpeed >= fleeSpeed * 0.9f)
                turn *= 1.7f;

            _currentDirection = Vector3.RotateTowards(
                _currentDirection.sqrMagnitude < 0.0001f ? _targetDirection : _currentDirection,
                _targetDirection,
                Mathf.Deg2Rad * turn * dt,
                0f);
            _currentDirection.y = 0f;
            if (_currentDirection.sqrMagnitude > 0.0001f)
                _currentDirection.Normalize();

            if (Time.time < _idleUntil && !_hasAttractTarget)
                desiredSpeed = wanderSpeed * 0.42f;

            _speed = Mathf.MoveTowards(_speed, desiredSpeed, dt * 3.4f);
            Move(_speed, dt);
        }

        private void TickRandomSwim()
        {
            if (Time.time < _idleUntil)
                return;

            if (_hasSwimTarget && ReachedSwimTarget())
            {
                _idleUntil = Time.time + Random.Range(0.05f, 0.22f);
                _hasSwimTarget = false;
                return;
            }

            if (!_hasSwimTarget || Time.time >= _retargetAt)
                PickRandomSwimTarget();

            var to = Flat(_swimTarget - transform.position);
            if (to.sqrMagnitude > 0.0001f)
                _targetDirection = to.normalized;
        }

        private bool ReachedSwimTarget()
        {
            var arrive = Mathf.Max(0.4f, roamRadius * 0.06f);
            return Flat(_swimTarget - transform.position).sqrMagnitude <= arrive * arrive;
        }

        private void PickRandomSwimTarget()
        {
            var minTravel = Mathf.Clamp(roamRadius * 0.28f, 0.7f, Mathf.Max(0.8f, roamRadius * 0.65f));
            Vector3 candidate;
            if (HomeSpot != null)
            {
                candidate = HomeSpot.RandomSwimPoint(transform.position, minTravel);
            }
            else
            {
                var offset = Random.insideUnitCircle * roamRadius;
                candidate = _spawnPosition + new Vector3(offset.x, 0f, offset.y);
            }

            PreferSpotAwayFromNeighbors(ref candidate);

            _swimTarget = candidate;
            _hasSwimTarget = true;
            _retargetAt = Time.time + Random.Range(2.2f, 4.8f);

            var to = Flat(_swimTarget - transform.position);
            if (to.sqrMagnitude > 0.0001f)
                _targetDirection = to.normalized;
        }

        private void PreferSpotAwayFromNeighbors(ref Vector3 candidate)
        {
            var avoidSq = neighborSeparation * neighborSeparation * 1.4f;
            for (var n = 0; n < 6; n++)
            {
                var crowded = false;
                for (var i = 0; i < All.Count; i++)
                {
                    var other = All[i];
                    if (other == null || other == this || other.IsCaught) continue;
                    if (other.HomeSpot != HomeSpot) continue;
                    var d = Flat(candidate - other.transform.position);
                    if (d.sqrMagnitude < avoidSq)
                    {
                        crowded = true;
                        break;
                    }
                }

                if (!crowded) return;
                if (HomeSpot != null)
                    candidate = HomeSpot.RandomSwimPoint(transform.position, Mathf.Max(0.7f, roamRadius * 0.28f));
            }
        }

        private void Move(float speed, float dt)
        {
            var prevYaw = _yaw;
            transform.position += _currentDirection.normalized * speed * dt;
            KeepInsideWater();

            if (_currentDirection.sqrMagnitude > 0.001f)
                _yaw = Mathf.Atan2(_currentDirection.x, _currentDirection.z) * Mathf.Rad2Deg;

            var yawRate = Mathf.DeltaAngle(prevYaw, _yaw) / Mathf.Max(0.0001f, dt);
            var roll = Mathf.Clamp(-yawRate * 0.12f, -bankDegrees, bankDegrees);
            var bob = Mathf.Sin((Time.time + _wobbleSeed) * bobFrequency) * bobAmplitude;
            var pitch = Mathf.Clamp(-bob * 55f, -10f, 10f);
            var look = Quaternion.Euler(pitch, _yaw, roll);
            transform.rotation = Quaternion.Slerp(transform.rotation, look, dt * 6f);

            var p = transform.position;
            p.y += bob;
            transform.position = p;
        }

        private void KeepInsideWater()
        {
            if (HomeSpot == null) return;
            var before = transform.position;
            var clamped = HomeSpot.ClampInside(before);
            var xz = Flat(clamped - before);
            transform.position = clamped;
            if (xz.sqrMagnitude > 0.04f)
            {
                var inward = Flat(HomeSpot.SwimCenter - transform.position);
                if (inward.sqrMagnitude > 0.0001f)
                    _targetDirection = inward.normalized;
            }
        }

        private void SeparateFromNeighbors()
        {
            var r = neighborSeparation;
            var rSq = r * r;
            var hard = r * 0.55f;
            for (var i = 0; i < All.Count; i++)
            {
                var other = All[i];
                if (other == null || other == this || other.IsCaught) continue;
                if (other.HomeSpot != HomeSpot) continue;
                var delta = Flat(transform.position - other.transform.position);
                var sq = delta.sqrMagnitude;
                if (sq > rSq) continue;
                if (sq < 0.0001f)
                {
                    var jitter = Quaternion.Euler(0f, (GetInstanceID() * 47f) % 360f, 0f) * Vector3.forward;
                    if (!_hasAttractTarget)
                        transform.position += jitter * 0.45f;
                    continue;
                }

                var dist = Mathf.Sqrt(sq);
                if (!_hasAttractTarget && dist < hard)
                    transform.position += (delta / dist) * (hard - dist) * 0.65f;
            }
        }

        public void TryCatchFromNet(Vector3 netCenter, float radius)
        {
            if (IsCaught || !CanCatchWith(FishCatchTool.Net))
                return;

            if (Vector3.Distance(Flat(transform.position), Flat(netCenter)) > radius)
                return;

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
