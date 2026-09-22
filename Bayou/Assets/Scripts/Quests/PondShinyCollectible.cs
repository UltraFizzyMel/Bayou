using System.Collections.Generic;
using Bayou.Inventory;
using Bayou.Player;
using Bayou.UI;
using UnityEngine;

namespace Bayou.Quests
{
    /// <summary>
    /// Rosary in the church pond. Scoop with the hand net (or a planted rod bobber)
    /// or stand next to it and press Interact.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PondShinyCollectible : MonoBehaviour, IInteractionPromptSource
    {
        private const string ResourcesItemPath = "Bayou/Items/Item_RosaryNecklace";
        private const string CanonicalItemId = "Item_RosaryNecklace";

        [SerializeField] private ItemDefinition item;
        [SerializeField] private float bobAmplitude = 0.08f;
        [SerializeField] private float bobSpeed = 2.2f;
        [SerializeField] private Color glowColor = new(0.95f, 0.85f, 0.35f, 1f);
        [SerializeField] private float extraCollectRadius = 0.85f;
        [SerializeField] private string pickupPrompt = "Pick up";
        [SerializeField] private float interactReach = 2.2f;

        private Vector3 _basePos;
        private bool _collected;
        private bool _playerInRange;
        [SerializeField]private Renderer _renderer;

        public ItemDefinition Item => item;
        public bool IsCollected => _collected;
        public static IReadOnlyList<PondShinyCollectible> Living => All;

        private static readonly List<PondShinyCollectible> All = new();

        private void Awake()
        {
            _basePos = transform.position;
            //_renderer = GetComponent<Renderer>();
            ResolveItem();
            ApplyGlow();
        }

        private void Start()
        {
            ResolveItem();
            ApplyGlow();
            Bayou.Rendering.WorldItemVisual.PatchRenderers(gameObject, force: true);
            EnsureSparkle();
        }

        private void EnsureSparkle()
        {
            if (GetComponentInChildren<ParticleSystem>(true) == null)
            {
                var go = new GameObject("Sparkle");
                go.transform.SetParent(transform, false);
                var ps = go.AddComponent<ParticleSystem>();
                var main = ps.main;
                main.loop = true;
                main.playOnAwake = true;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.9f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.28f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.16f);
                main.startColor = glowColor;
                main.maxParticles = 28;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                var emission = ps.emission;
                emission.rateOverTime = 14f;
                var shape = ps.shape;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 0.22f;
                ps.Play();
            }

            SoftenSparkles();
            EnsureGlint();
        }

        private void EnsureGlint()
        {
            if (transform.Find("Glint") != null) return;

            var root = new GameObject("Glint");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = new Vector3(0f, 0.55f, 0f);
            var s = transform.lossyScale;
            root.transform.localScale = new Vector3(
                1f / Mathf.Max(0.05f, Mathf.Abs(s.x)),
                1f / Mathf.Max(0.05f, Mathf.Abs(s.y)),
                1f / Mathf.Max(0.05f, Mathf.Abs(s.z)));
            var mat = Bayou.Rendering.BayouShaderUtil.CreateUnlitColor(new Color(1f, 0.95f, 0.65f, 1f));
            AddGlintQuad(root.transform, mat, Quaternion.identity);
            AddGlintQuad(root.transform, mat, Quaternion.Euler(0f, 90f, 0f));
        }

        private void PulseGlint()
        {
            var glint = transform.Find("Glint");
            if (glint == null) return;
            glint.Rotate(0f, 90f * Time.deltaTime, 0f, Space.Self);
            var pulse = 0.85f + 0.35f * Mathf.Sin(Time.time * 5.5f);
            for (var i = 0; i < glint.childCount; i++)
                glint.GetChild(i).localScale = new Vector3(0.16f, 0.48f * pulse, 0.16f);
        }

        private static void AddGlintQuad(Transform parent, Material mat, Quaternion localRot)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "GlintFlare";
            go.transform.SetParent(parent, false);
            go.transform.localRotation = localRot;
            go.transform.localScale = new Vector3(0.22f, 0.55f, 0.22f);
            var col = go.GetComponent<Collider>();
            if (col != null)
                Destroy(col);
            var rend = go.GetComponent<MeshRenderer>();
            rend.sharedMaterial = mat;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;
        }

        private void OnEnable()
        {
            if (!All.Contains(this))
                All.Add(this);
            InteractionPromptBroker.Register(this);
            InputManager.GetInstance()?.RegisterInteractPressed();
        }

        private void OnDisable()
        {
            All.Remove(this);
            InteractionPromptBroker.Unregister(this);
        }

        private void Update()
        {
            if (_collected) return;

            var y = _basePos.y + Mathf.Sin(Time.time * bobSpeed) * bobAmplitude;
            transform.position = new Vector3(_basePos.x, y, _basePos.z);
            PulseGlint();

            if (!CanCollectNow())
                return;
            if (!_playerInRange || DistToPlayerSq() > interactReach * interactReach)
                return;

            var dialogue = DialogueManager.GetInstance();
            if (dialogue != null && dialogue.dialogueIsPlaying)
                return;

            var input = InputManager.GetInstance();
            if (input != null && input.GetInteractPressed())
                Collect();
        }

        public bool TryCollectFromNet(Vector3 netPos, float radius)
        {
            if (_collected || !CanCollectNow()) return false;
            ResolveItem();
            if (item == null) return false;

            var reach = Mathf.Max(0.6f, radius) + extraCollectRadius;
            var flat = transform.position - netPos;
            flat.y = 0f;
            if (flat.sqrMagnitude > reach * reach)
                return false;

            return Collect();
        }

        private static bool CanCollectNow() => Time.timeSinceLevelLoad >= 0.45f;

        public static bool TryScoopNear(Vector3 netPos, float radius)
        {
            var all = Living;
            for (var i = 0; i < all.Count; i++)
            {
                var shiny = all[i];
                if (shiny != null && shiny.TryCollectFromNet(netPos, radius))
                    return true;
            }

            return false;
        }

        public bool TryGetInteractionPrompt(out InteractionPrompt prompt)
        {
            prompt = default;
            if (!CanCollectNow() || !_playerInRange || _collected || item == null)
                return false;
            if (DistToPlayerSq() > interactReach * interactReach)
                return false;

            var name = string.IsNullOrWhiteSpace(item.displayName) ? "rosary" : item.displayName;
            var action = string.IsNullOrWhiteSpace(pickupPrompt) ? $"Pick up {name}" : $"{pickupPrompt} {name}";
            prompt = new InteractionPrompt("E", action.Trim(), 70, DistToPlayerSq());
            return true;
        }

        private bool Collect()
        {
            if (!CanCollectNow())
                return false;

            ResolveItem();
            if (_collected || item == null)
            {
                if (item == null)
                    Debug.LogError("[PondRosary] Cannot collect — Item_RosaryNecklace is missing.");
                return false;
            }

            _collected = true;
            Debug.Log($"[PondRosary] Collected {item.displayName}.");
            CaughtFishPresenter.Present(item);
            Destroy(gameObject);
            return true;
        }

        private void ResolveItem()
        {
            if (item != null && item.MatchesId(CanonicalItemId))
                return;

            var loaded = Resources.Load<ItemDefinition>(ResourcesItemPath);
            if (loaded == null)
                loaded = Resources.Load<ItemDefinition>("Bayou/Items/Item_ShinyPond");
            if (loaded != null)
                item = loaded;
        }

        private void ApplyGlow()
        {
            if (_renderer == null)
                _renderer = GetComponent<MeshRenderer>();
            if (_renderer == null) return;
            _renderer.sharedMaterial = Bayou.Rendering.BayouShaderUtil.CreateUnlitColor(glowColor);
            _renderer.enabled = true;
        }

        private void SoftenSparkles()
        {
            var particles = GetComponentsInChildren<ParticleSystem>(true);
            for (var i = 0; i < particles.Length; i++)
            {
                var ps = particles[i];
                if (ps == null) continue;

                var main = ps.main;
                main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
                main.maxParticles = Mathf.Min(main.maxParticles, 32);
                main.simulationSpace = ParticleSystemSimulationSpace.World;

                var rend = ps.GetComponent<ParticleSystemRenderer>();
                if (rend == null) continue;
                var sparkle = Resources.Load<Material>("Bayou/RuntimeSparkle");
                if (sparkle != null)
                    rend.sharedMaterial = sparkle;
                rend.enabled = true;
                rend.renderMode = ParticleSystemRenderMode.Billboard;
                rend.maxParticleSize = 0.5f;
                rend.minParticleSize = 0f;
                rend.allowRoll = false;
                if (!ps.isPlaying)
                    ps.Play();
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (IsPlayer(other))
                _playerInRange = true;
        }

        private void OnTriggerExit(Collider other)
        {
            if (IsPlayer(other))
                _playerInRange = false;
        }

        private static bool IsPlayer(Collider other)
        {
            return other.CompareTag("Player") ||
                   other.TryGetComponent<BayouCharacterMotor>(out _) ||
                   other.GetComponentInParent<BayouCharacterMotor>() != null;
        }

        private float DistToPlayerSq()
        {
            var p = PlayerLocator.Transform;
            if (p == null) return 0f;
            var d = transform.position - p.position;
            d.y = 0f;
            return d.sqrMagnitude;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (item == null || !item.MatchesId(CanonicalItemId))
            {
                item = UnityEditor.AssetDatabase.LoadAssetAtPath<ItemDefinition>(
                    "Assets/Inventory/Items/Item_RosaryNecklace.asset");
            }
        }
#endif
    }
}
