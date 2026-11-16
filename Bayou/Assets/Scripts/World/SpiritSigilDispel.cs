using System.Collections.Generic;
using Bayou.Inventory;
using Bayou.Player;
using Bayou.UI;
using UnityEngine;

namespace Bayou.World
{
    /// <summary>
    /// Spirit lantern dispels a floating sigil. Fades visuals and disables this sigil's barrier collider.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SpiritSigilDispel : MonoBehaviour, IInteractionPromptSource
    {
        private const float Reach = 6.5f;
        private const float FadeSeconds = 2.2f;

        private static readonly List<SpiritSigilDispel> All = new();
        public static int DispelledCount { get; private set; }
        public static int TotalCount => All.Count;
        public static bool AllDispelled => TotalCount > 0 && DispelledCount >= TotalCount;

        public static event System.Action SigilStateChanged;

        [SerializeField] private Collider barrierCollider;

        private readonly List<Renderer> _renderers = new();
        private bool _dispelled;
        private float _fadeUntil;
        private Color[] _startColors;
        private MaterialPropertyBlock _mpb;
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private void Awake()
        {
            CollectRenderers();
            if (barrierCollider == null)
            {
                barrierCollider = GetComponent<Collider>();
                if (barrierCollider == null)
                    barrierCollider = gameObject.AddComponent<BoxCollider>();
            }

            if (barrierCollider != null)
                barrierCollider.enabled = false;
        }

        private void OnEnable()
        {
            if (!All.Contains(this))
                All.Add(this);
            InteractionPromptBroker.Register(this);
            RefreshGlobalCount();
        }

        private void OnDisable()
        {
            All.Remove(this);
            InteractionPromptBroker.Unregister(this);
            RefreshGlobalCount();
        }

        private void Update()
        {
            if (_dispelled)
            {
                TickFade();
                return;
            }

            if (!HasSpiritLantern()) return;
            if (GameplayPause.BlocksWorldInteract || GameplayPause.IsDialoguePlaying) return;
            if (!InRange()) return;

            var input = InputManager.GetInstance();
            if (input == null || !input.GetInteractPressed())
                return;

            Dispel();
        }

        public bool TryGetInteractionPrompt(out InteractionPrompt prompt)
        {
            prompt = default;
            if (_dispelled || !HasSpiritLantern() || !InRange()) return false;
            if (GameplayPause.BlocksWorldInteract || GameplayPause.IsDialoguePlaying) return false;
            prompt = new InteractionPrompt("E", "Banish the sigil", 58, DistanceSq());
            return true;
        }

        private void Dispel()
        {
            if (_dispelled) return;
            _dispelled = true;
            _fadeUntil = Time.time + FadeSeconds;
            RefreshGlobalCount();
            if (barrierCollider != null)
                barrierCollider.enabled = false;

            var spin = GetComponent<SigilSpin>();
            if (spin != null) spin.enabled = false;
            var bob = GetComponent<ObjectBobbing>();
            if (bob != null) bob.enabled = false;

            SigilStateChanged?.Invoke();
        }

        private void TickFade()
        {
            if (_renderers.Count == 0)
            {
                if (Time.time >= _fadeUntil)
                    gameObject.SetActive(false);
                return;
            }

            var t = 1f - Mathf.Clamp01((_fadeUntil - Time.time) / FadeSeconds);
            _mpb ??= new MaterialPropertyBlock();
            for (var i = 0; i < _renderers.Count; i++)
            {
                var r = _renderers[i];
                if (r == null) continue;
                var c = _startColors[i];
                c.a = Mathf.Lerp(c.a, 0f, t);
                ApplyFadeColor(r, c);
            }

            if (Time.time >= _fadeUntil)
                gameObject.SetActive(false);
        }

        private void CollectRenderers()
        {
            _renderers.Clear();
            var rs = GetComponentsInChildren<Renderer>(true);
            for (var i = 0; i < rs.Length; i++)
            {
                if (rs[i] != null)
                    _renderers.Add(rs[i]);
            }

            _startColors = new Color[_renderers.Count];
            _mpb ??= new MaterialPropertyBlock();
            for (var i = 0; i < _renderers.Count; i++)
                _startColors[i] = ReadColor(_renderers[i], _mpb);
        }

        private static Color ReadColor(Renderer r, MaterialPropertyBlock block)
        {
            r.GetPropertyBlock(block);
            if (block.isEmpty)
            {
                var mat = r.sharedMaterial;
                if (mat == null) return Color.white;
                if (mat.HasProperty(BaseColorId)) return mat.GetColor(BaseColorId);
                if (mat.HasProperty(ColorId)) return mat.GetColor(ColorId);
                return mat.color;
            }

            if (block.HasColor(BaseColorId)) return block.GetColor(BaseColorId);
            if (block.HasColor(ColorId)) return block.GetColor(ColorId);
            return Color.white;
        }

        private void ApplyFadeColor(Renderer r, Color c)
        {
            r.GetPropertyBlock(_mpb);
            if (r.sharedMaterial != null && r.sharedMaterial.HasProperty(BaseColorId))
                _mpb.SetColor(BaseColorId, c);
            else if (r.sharedMaterial != null && r.sharedMaterial.HasProperty(ColorId))
                _mpb.SetColor(ColorId, c);
            else
                _mpb.SetColor(ColorId, c);
            r.SetPropertyBlock(_mpb);
        }

        private bool InRange()
        {
            var player = PlayerLocator.Transform;
            if (player == null) return false;
            return DistanceSq() <= Reach * Reach;
        }

        private float DistanceSq()
        {
            var player = PlayerLocator.Transform;
            if (player == null) return float.MaxValue;
            var delta = transform.position - player.position;
            delta.y *= 0.4f;
            return delta.sqrMagnitude;
        }

        private static bool HasSpiritLantern()
        {
            var inv = InventoryController.Instance;
            return inv != null && inv.CountExact("Item_SpiritLantern") >= 1;
        }

        private static void RefreshGlobalCount()
        {
            var count = 0;
            for (var i = 0; i < All.Count; i++)
            {
                if (All[i] != null && All[i]._dispelled)
                    count++;
            }

            DispelledCount = count;
        }
    }
}