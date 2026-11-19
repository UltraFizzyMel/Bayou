using Bayou.Player;
using UnityEngine;

namespace Bayou.Environment
{
    /// <summary>
    /// Trigger volume for an outer-bound river. Pushes the player downstream, then
    /// kills them through PlayerHealth so the death screen still appears.
    /// Authored triggers can be added in the editor. TerrainTest also installs two
    /// river-only triggers, with a water-depth check to protect nearby shore paths.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(500)]
    public sealed class WashAwayCurrent : MonoBehaviour
    {
        [SerializeField] private Vector3 direction = new Vector3(0f, 0f, 1f);
        [SerializeField] private float acceleration = 36f;
        [SerializeField] private float delayBeforeDeath = 2.4f;
        [SerializeField] private float distanceBeforeDeath = 8f;
        private bool _requireWater;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallSceneCurrents()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= InstallSceneCurrents;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += InstallSceneCurrents;
            InstallSceneCurrents(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),
                UnityEngine.SceneManagement.LoadSceneMode.Single);
        }

        private static void InstallSceneCurrents(UnityEngine.SceneManagement.Scene scene,
            UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            if (scene.name != "TerrainTest") return;
            AddRiverCurrent("River Water (1)", new Vector3(16f, 4f, 6f), Vector3.right);
            AddRiverCurrent("River Water (2)", new Vector3(10f, 4f, 3f), Vector3.right);
        }

        private static void AddRiverCurrent(string surfaceName, Vector3 size, Vector3 direction)
        {
            var surface = GameObject.Find(surfaceName);
            if (surface == null || surface.transform.Find("Fast Current Trigger") != null) return;

            var zone = new GameObject("Fast Current Trigger");
            zone.transform.SetParent(surface.transform, true);
            zone.transform.position = surface.transform.position;
            var trigger = zone.AddComponent<BoxCollider>();
            trigger.size = size;
            trigger.isTrigger = true;
            var current = zone.AddComponent<WashAwayCurrent>();
            current.direction = direction;
            current.distanceBeforeDeath = float.MaxValue;
            current._requireWater = true;
        }

        [Tooltip("Optional water particles. Left inactive until assigned, then played only while the player is in the current.")]
        [SerializeField] private GameObject CurrentEffect;

        private const float SweepCommitDistance = 0.75f;

        private Rigidbody _playerBody;
        private BayouCharacterMotor _player;
        private Vector3 _enterPos;
        private float _enteredAt;
        private bool _killed;
        private bool _swept;

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
            if (CurrentEffect != null)
                CurrentEffect.SetActive(false);
        }

        private void OnTriggerStay(Collider other)
        {
            if (_player == null) OnTriggerEnter(other);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!TryGetPlayer(other, out var motor, out var body)) return;
            if (_requireWater && (!motor.TryGetComponent<BayouWaterSensor>(out var water) || !water.InWater))
                return;
            var continuing = _player == motor && _swept && !_killed;
            _player = motor;
            _playerBody = body;
            if (!continuing)
            {
                _enterPos = motor.transform.position;
                _enteredAt = Time.time;
                _killed = false;
                _swept = false;
            }

            SetEffect(true);
        }

        private void OnTriggerExit(Collider other)
        {
            if (!TryGetPlayer(other, out var motor, out _)) return;
            if (_player != null && motor != _player) return;
            SetEffect(false);
            // The current has already taken them. Leaving the volume does not save them.
            if (!_requireWater && _swept && !_killed)
                return;
            _player = null;
            _playerBody = null;
        }

        private void FixedUpdate()
        {
            if (_killed || _playerBody == null || _player == null) return;
            if (!_playerBody.gameObject.activeInHierarchy ||
                (_requireWater && (!_player.TryGetComponent<BayouWaterSensor>(out var water) || !water.InWater)))
            {
                Clear();
                return;
            }

            var dir = FlatDirection();
            var velocity = _playerBody.linearVelocity;
            var planar = new Vector3(velocity.x, 0f, velocity.z);
            var downstream = Vector3.Dot(planar, dir);
            var step = Mathf.Max(0.5f, acceleration) * Time.fixedDeltaTime;
            var cap = _requireWater ? 2.5f : Mathf.Max(4.5f, acceleration * 0.22f);
            downstream = Mathf.MoveTowards(downstream, cap, step);
            var lateral = planar - dir * Vector3.Dot(planar, dir);
            lateral = Vector3.MoveTowards(lateral, Vector3.zero, step * 0.65f);
            var next = dir * downstream + lateral;
            _playerBody.linearVelocity = new Vector3(next.x, velocity.y, next.z);

            var carried = Vector3.Dot(_player.transform.position - _enterPos, dir);
            if (carried >= SweepCommitDistance)
                _swept = true;

            var waited = Time.time - _enteredAt;
            if (waited < Mathf.Max(0.2f, delayBeforeDeath) &&
                carried < Mathf.Max(0.5f, distanceBeforeDeath))
                return;

            var health = _player.GetComponent<PlayerHealth>() ?? PlayerHealth.Resolve();
            if (health == null || !health.IsAlive)
            {
                if (health != null)
                    Clear();
                return;
            }

            _killed = true;
            health.KillByHazard(PlayerHealth.FastWaterDeathTip);
            Clear();
        }

        private void Clear()
        {
            _player = null;
            _playerBody = null;
            _swept = false;
            SetEffect(false);
        }

        private void SetEffect(bool on)
        {
            if (CurrentEffect != null && CurrentEffect.activeSelf != on)
                CurrentEffect.SetActive(on);
        }

        private Vector3 FlatDirection()
        {
            var d = direction;
            d.y = 0f;
            if (d.sqrMagnitude < 0.0001f)
            {
                d = transform.forward;
                d.y = 0f;
            }

            if (d.sqrMagnitude < 0.0001f)
                d = Vector3.forward;
            return d.normalized;
        }

        private static bool TryGetPlayer(Collider other, out BayouCharacterMotor motor, out Rigidbody body)
        {
            motor = null;
            body = null;
            if (other == null) return false;
            motor = other.GetComponent<BayouCharacterMotor>() ?? other.GetComponentInParent<BayouCharacterMotor>();
            if (motor == null) return false;
            body = motor.GetComponent<Rigidbody>();
            return body != null;
        }
    }
}
