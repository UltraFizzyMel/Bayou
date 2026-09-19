using System;
using System.Collections;
using Bayou.Audio;
using Bayou.CameraControl;
using Bayou.Combat;
using Bayou.Creatures;
using Bayou.Save;
using UnityEngine;

namespace Bayou.Player
{
    /// <summary>
    /// Player hit points. Damage comes from creature contact; the only heal is cooking fish at a campfire.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerHealth : MonoBehaviour, IPlayerHurtReceiver
    {
        public const int DefaultMaxHealth = 5;

        [SerializeField] private int maxHealth = DefaultMaxHealth;
        [SerializeField] private float invulnerabilitySeconds = 0.9f;
        [SerializeField] private float stunSeconds = 0.42f;
        [SerializeField] private float knockbackScale = 1f;
        [SerializeField] private float deathHoldSeconds = 1.15f;
        [SerializeField] private AudioClip hurtClip;

        private int _current;
        private float _invulnerableUntil;
        private bool _dying;
        private Coroutine _flashRoutine;
        private Renderer[] _flashRenderers = Array.Empty<Renderer>();
        private Color[] _flashColors = Array.Empty<Color>();
        private int[] _flashPropIds = Array.Empty<int>();
        private SfxPlayer _sfx;
        private BayouCharacterMotor _motor;

        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int UnlitColorId = Shader.PropertyToID("_UnlitColor");

        public static PlayerHealth Instance { get; private set; }

        public int Current => _current;
        public int Max => Mathf.Max(1, maxHealth);
        public float Normalized => Max <= 0 ? 0f : (float)_current / Max;
        public bool IsAlive => _current > 0 && !_dying;
        public bool IsDying => _dying;

        public event Action Changed;

        public static bool BlocksAction
        {
            get
            {
                if (Instance != null && !Instance.IsAlive)
                    return true;
                var motor = PlayerLocator.Motor;
                return motor != null && motor.IsStunned;
            }
        }

        public static PlayerHealth Resolve()
        {
            if (Instance != null) return Instance;
            var motor = PlayerLocator.Motor;
            if (motor != null)
                return EnsureOn(motor.gameObject);
            return FindFirstObjectByType<PlayerHealth>();
        }

        public static PlayerHealth EnsureOn(GameObject player)
        {
            if (player == null) return null;
            var health = player.GetComponent<PlayerHealth>();
            if (health == null)
                health = player.AddComponent<PlayerHealth>();
            health.enabled = true;
            return health;
        }

        private void Awake()
        {
            Instance = this;
            _motor = GetComponent<BayouCharacterMotor>();
            _sfx = GetComponent<SfxPlayer>();
            if (_current <= 0)
                _current = Max;
        }

        private void OnEnable()
        {
            Instance = this;
            if (_current <= 0 && !_dying)
                _current = Max;
            Changed?.Invoke();
        }

        private void OnDestroy()
        {
            RestoreFlashColors();
            if (Instance == this)
                Instance = null;
        }

        public void HealToFull()
        {
            if (_dying) return;
            var next = Max;
            if (_current == next) return;
            _current = next;
            Changed?.Invoke();
        }

        public void Restore(int current, int max)
        {
            if (max > 0)
                maxHealth = max;
            _current = Mathf.Clamp(current, 0, Max);
            if (_current <= 0)
                _current = 1;
            _dying = false;
            Changed?.Invoke();
        }

        public void OnCreatureHit(CreatureHitInfo info)
        {
            if (!IsAlive) return;
            if (Time.time < _invulnerableUntil) return;

            var damage = Mathf.Max(1, Mathf.RoundToInt(info.Damage));
            _current = Mathf.Max(0, _current - damage);
            _invulnerableUntil = Time.time + invulnerabilitySeconds;
            Changed?.Invoke();

            var knock = info.KnockbackVelocity * knockbackScale;
            if (_motor != null)
                _motor.ApplyHitReaction(knock, stunSeconds);
            else
            {
                var rb = GetComponent<Rigidbody>();
                if (rb != null && !rb.isKinematic)
                    rb.AddForce(knock, ForceMode.VelocityChange);
            }

            CombatFeedback.PlayPlayerHurt(transform.position, knock);
            PlayHurtFeedback();

            if (_current <= 0)
                StartCoroutine(DieRoutine());
        }

        private void PlayHurtFeedback()
        {
            if (_motor != null)
                _motor.PlayHurtAnimation();

            if (hurtClip != null)
            {
                if (_sfx == null)
                    _sfx = GetComponent<SfxPlayer>() ?? gameObject.AddComponent<SfxPlayer>();
                _sfx.PlayOneShotPitched(hurtClip, 0.9f, UnityEngine.Random.Range(0.92f, 1.08f));
            }

            if (_flashRoutine != null)
                StopCoroutine(_flashRoutine);
            _flashRoutine = StartCoroutine(FlashRoutine());
        }

        private IEnumerator FlashRoutine()
        {
            CacheFlashTargets();
            ApplyFlashTint(new Color(1f, 0.22f, 0.18f, 1f));
            yield return new WaitForSeconds(0.12f);
            RestoreFlashColors();
            _flashRoutine = null;
        }

        private IEnumerator DieRoutine()
        {
            _dying = true;
            Changed?.Invoke();

            if (_motor != null)
                _motor.ApplyHitReaction(Vector3.zero, deathHoldSeconds);

            yield return new WaitForSeconds(deathHoldSeconds);

            var save = GameSaveSystem.Instance;
            if (save != null)
                save.RespawnAfterDeath();
            else
            {
                Restore(Max, Max);
                var cam = FindFirstObjectByType<BayouFollowCamera>();
                cam?.SnapToTarget();
            }

            if (_current <= 0)
                Restore(Max, Max);

            _dying = false;
            _invulnerableUntil = Time.time + 1.4f;
            Changed?.Invoke();
        }

        private void CacheFlashTargets()
        {
            var renderers = GetComponentsInChildren<Renderer>(true);
            var count = 0;
            for (var i = 0; i < renderers.Length; i++)
            {
                if (IsFlashable(renderers[i]))
                    count++;
            }

            _flashRenderers = new Renderer[count];
            _flashColors = new Color[count];
            _flashPropIds = new int[count];
            var n = 0;
            for (var i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (!IsFlashable(r) || r.sharedMaterial == null) continue;
                var shared = r.sharedMaterial;
                var prop = ColorId;
                if (shared.HasProperty(BaseColorId)) prop = BaseColorId;
                else if (shared.HasProperty(UnlitColorId)) prop = UnlitColorId;
                else if (!shared.HasProperty(ColorId))
                    continue;

                var mat = r.material;
                _flashRenderers[n] = r;
                _flashPropIds[n] = prop;
                _flashColors[n] = mat.GetColor(prop);
                n++;
            }

            if (n == _flashRenderers.Length) return;
            Array.Resize(ref _flashRenderers, n);
            Array.Resize(ref _flashColors, n);
            Array.Resize(ref _flashPropIds, n);
        }

        private void ApplyFlashTint(Color color)
        {
            for (var i = 0; i < _flashRenderers.Length; i++)
            {
                var r = _flashRenderers[i];
                if (r == null) continue;
                r.material.SetColor(_flashPropIds[i], color);
            }
        }

        private void RestoreFlashColors()
        {
            for (var i = 0; i < _flashRenderers.Length; i++)
            {
                var r = _flashRenderers[i];
                if (r == null) continue;
                r.material.SetColor(_flashPropIds[i], _flashColors[i]);
            }
        }

        private static bool IsFlashable(Renderer renderer)
        {
            if (renderer == null) return false;
            return renderer is MeshRenderer or SkinnedMeshRenderer;
        }
    }
}
