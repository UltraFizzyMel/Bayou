using System.Collections.Generic;
using Bayou.Inventory;
using Bayou.UI;
using UnityEngine;

namespace Bayou.World
{
    /// <summary>
    /// Lantern lights a seal altar: intact mesh, burning fire, then burnt mesh. Writes ink/world flags only for altars.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(20)]
    public sealed class AltarBurn : MonoBehaviour, IInteractionPromptSource
    {
        public const string FoggyFlag = BurnableInteract.FoggyFlag;
        public const string GraveFlag = BurnableInteract.GraveFlag;
        public const string BrackishFlag = BurnableInteract.BrackishFlag;

        private const float Reach = 4.25f;
        private const float BurnSeconds = 5f;

        private enum Phase
        {
            Intact,
            Burning,
            Burnt
        }

        private static readonly List<AltarBurn> Active = new();

        [SerializeField] private string inkFlag = "";
        [SerializeField] private GameObject intact;
        [SerializeField] private GameObject burning;
        [SerializeField] private GameObject burnt;
        [SerializeField] private GameObject fireEffect;

        private GameObject _fireEffectsRoot;
        private readonly List<ParticleSystem> _fireSystems = new();
        private Phase _phase = Phase.Intact;
        private float _burnUntil;
        private Collider[] _colliders = System.Array.Empty<Collider>();

        public bool IsBurned => _phase == Phase.Burnt;
        public string InkFlag => inkFlag ?? "";

        public static bool HasAltarAtMost(float distSq)
        {
            for (var i = 0; i < Active.Count; i++)
            {
                var altar = Active[i];
                if (altar == null || altar._phase == Phase.Burnt) continue;
                if (!HasLantern() || !altar.InRange()) continue;
                if (altar.DistanceSq() <= distSq)
                    return true;
            }

            return false;
        }

        public void Configure(string flag)
        {
            inkFlag = flag ?? "";
            ResolveVisuals();
            var burned = ReadBurned();
            Apply(burned ? Phase.Burnt : Phase.Intact);
        }

        private void Awake()
        {
            ResolveVisuals();
            _colliders = GetComponentsInChildren<Collider>(true);
            Apply(ReadBurned() ? Phase.Burnt : Phase.Intact);
        }

        private void OnEnable()
        {
            if (!Active.Contains(this))
                Active.Add(this);
            InteractionPromptBroker.Register(this);
        }

        private void OnDisable()
        {
            Active.Remove(this);
            InteractionPromptBroker.Unregister(this);
        }

        private void Update()
        {
            if (_phase == Phase.Burning)
            {
                if (Time.time >= _burnUntil)
                    FinishBurn();
                return;
            }

            if (_phase != Phase.Intact) return;
            if (!HasLantern()) return;
            if (Bayou.GameplayPause.BlocksWorldInteract || Bayou.GameplayPause.IsDialoguePlaying)
                return;

            var flower = GetComponent<GraveFlowerPlacement>();
            if (flower != null && flower.ClaimsInteract)
                return;

            if (BrambleBurn.TookInteractThisFrame) return;
            if (!InRange()) return;

            var input = InputManager.GetInstance();
            if (input == null || !input.GetInteractPressed())
                return;

            Ignite();
        }

        public bool TryGetInteractionPrompt(out InteractionPrompt prompt)
        {
            prompt = default;
            if (_phase != Phase.Intact || !HasLantern()) return false;
            if (Bayou.GameplayPause.BlocksWorldInteract || Bayou.GameplayPause.IsDialoguePlaying)
                return false;

            var flower = GetComponent<GraveFlowerPlacement>();
            if (flower != null && flower.ClaimsInteract)
                return false;

            if (!InRange()) return false;
            prompt = new InteractionPrompt("E", "Light the altar", 60, DistanceSq());
            return true;
        }

        private void Ignite()
        {
            if (_phase != Phase.Intact) return;
            _burnUntil = Time.time + BurnSeconds;
            Apply(Phase.Burning);
        }

        private void FinishBurn()
        {
            if (!string.IsNullOrWhiteSpace(inkFlag))
            {
                WorldFlags.Set(inkFlag, true);
                DialogueManager.GetInstance()?.SetBoolVariable(inkFlag, true);
            }

            Apply(Phase.Burnt);
            TryAdvanceSeals();
        }

        private void TryAdvanceSeals()
        {
            if (!WorldFlags.Has(FoggyFlag) || !WorldFlags.Has(GraveFlag) || !WorldFlags.Has(BrackishFlag))
                return;

            QuestManager.Resolve()?.MarkReadyToFinish(QuestIds.BreakTheSeals);
        }

        private bool ReadBurned()
        {
            return !string.IsNullOrWhiteSpace(inkFlag) && WorldFlags.Has(inkFlag);
        }

        private void Apply(Phase phase)
        {
            _phase = phase;
            var hasBurningMesh = burning != null;

            SetActive(intact, phase == Phase.Intact || (phase == Phase.Burning && !hasBurningMesh));
            SetActive(burning, phase == Phase.Burning && hasBurningMesh);
            SetActive(burnt, phase == Phase.Burnt);
            SetFireEffectsActive(phase == Phase.Burning);
        }

        private void ResolveVisuals()
        {
            var transforms = GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < transforms.Length; i++)
            {
                var t = transforms[i];
                if (t == null) continue;
                var name = t.name;

                if (burnt == null && IsBurntVisualName(name))
                    burnt = t.gameObject;
                else if (intact == null && IsIntactVisualName(name))
                    intact = t.gameObject;
                else if (burning == null && IsState(name, "Burning"))
                    burning = t.gameObject;
                else if (_fireEffectsRoot == null && IsFireEffectsName(name))
                    _fireEffectsRoot = t.gameObject;
                else if (fireEffect == null && string.Equals(name, "FireEffect", System.StringComparison.OrdinalIgnoreCase))
                    fireEffect = t.gameObject;
            }

            if (fireEffect == null)
                fireEffect = _fireEffectsRoot;

            CollectFireSystems();
            SetFireEffectsActive(false);
        }

        private static bool IsIntactVisualName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            if (name.IndexOf("Burnt", System.StringComparison.OrdinalIgnoreCase) >= 0) return false;
            return name.IndexOf("AltarMaterials", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsBurntVisualName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            return name.IndexOf("BurntAltar", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   (name.IndexOf("Burnt", System.StringComparison.OrdinalIgnoreCase) >= 0 &&
                    name.IndexOf("Altar", System.StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static bool IsFireEffectsName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            if (string.Equals(name, "FireEffect", System.StringComparison.OrdinalIgnoreCase))
                return true;
            return name.IndexOf("fire effect", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void CollectFireSystems()
        {
            _fireSystems.Clear();

            if (_fireEffectsRoot != null)
                AddParticleSystemsFrom(_fireEffectsRoot);

            if (fireEffect != null && fireEffect != _fireEffectsRoot)
                AddParticleSystemsFrom(fireEffect);

            if (_fireSystems.Count == 0)
            {
                for (var i = 0; i < transform.childCount; i++)
                {
                    var child = transform.GetChild(i);
                    if (child == null) continue;
                    AddParticleSystemsFrom(child.gameObject);
                }
            }
        }

        private void AddParticleSystemsFrom(GameObject root)
        {
            if (root == null) return;
            var systems = root.GetComponentsInChildren<ParticleSystem>(true);
            for (var i = 0; i < systems.Length; i++)
            {
                if (systems[i] != null && !_fireSystems.Contains(systems[i]))
                    _fireSystems.Add(systems[i]);
            }
        }

        private void SetFireEffectsActive(bool show)
        {
            if (_fireEffectsRoot != null &&
                _fireEffectsRoot != intact &&
                _fireEffectsRoot != burning &&
                _fireEffectsRoot != burnt)
            {
                if (_fireEffectsRoot.activeSelf != show)
                    _fireEffectsRoot.SetActive(show);
            }
            else if (fireEffect != null &&
                     fireEffect != intact &&
                     fireEffect != burning &&
                     fireEffect != burnt &&
                     fireEffect != _fireEffectsRoot)
            {
                if (fireEffect.activeSelf != show)
                    fireEffect.SetActive(show);
            }

            for (var i = 0; i < _fireSystems.Count; i++)
            {
                var ps = _fireSystems[i];
                if (ps == null) continue;

                if (show)
                {
                    if (!ps.gameObject.activeInHierarchy)
                        ps.gameObject.SetActive(true);
                    if (!ps.isPlaying)
                        ps.Play(true);
                }
                else
                {
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }
        }

        private static bool IsState(string name, params string[] tokens)
        {
            if (string.IsNullOrEmpty(name)) return false;
            for (var i = 0; i < tokens.Length; i++)
            {
                if (name.IndexOf(tokens[i], System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }

        private static void SetActive(GameObject go, bool on)
        {
            if (go != null && go.activeSelf != on)
                go.SetActive(on);
        }

        private bool InRange()
        {
            var player = Bayou.Player.PlayerLocator.Transform;
            if (player == null) return false;
            return DistanceSq() <= Reach * Reach;
        }

        private Vector3 InteractionPoint()
        {
            if (_colliders != null)
            {
                for (var i = 0; i < _colliders.Length; i++)
                {
                    var col = _colliders[i];
                    if (col != null && col.enabled)
                        return col.bounds.center;
                }
            }

            return transform.position;
        }

        private float DistanceSq()
        {
            var player = Bayou.Player.PlayerLocator.Transform;
            if (player == null) return float.MaxValue;
            var delta = InteractionPoint() - player.position;
            delta.y = 0f;
            return delta.sqrMagnitude;
        }

        private static bool HasLantern()
        {
            var inv = InventoryController.Instance;
            if (inv == null) return false;
            return inv.HasItemsById("Item_Lantern", 1) || inv.HasItemsById("Item_SpiritLantern", 1);
        }
    }
}