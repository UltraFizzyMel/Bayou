using System.Collections.Generic;
using Bayou.Environment;
using Bayou.Inventory;
using Bayou.Player;
using Bayou.Save;
using Bayou.UI;
using UnityEngine;

namespace Bayou.World
{
    /// <summary>
    /// Lantern sets the nearest bramble alight. Fire spreads to neighbors after a short delay.
    /// Intact, burning, and burnt are the prefab's existing child visuals. No altar flags.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-40)]
    public sealed class BrambleBurn : MonoBehaviour, IInteractionPromptSource
    {
        private const float Reach = 4.25f;
        private const float ChainReach = 6.5f;
        private const float ChainDelay = 0.8f;
        private const float BurnSeconds = 4.2f;

        private enum Phase
        {
            Intact,
            Burning,
            Burnt
        }

        private static readonly List<BrambleBurn> All = new();
        private static int _claimedFrame = -1;

        public static bool TookInteractThisFrame => _claimedFrame == Time.frameCount;

        [SerializeField] private GameObject intact;
        [SerializeField] private GameObject burning;
        [SerializeField] private GameObject burnt;
        [SerializeField] private GameObject fireEffect;
        [SerializeField] private ParticleSystem fireParticles;

        private GameObject _fireEffectsRoot;
        private readonly List<ParticleSystem> _fireSystems = new();
        private Phase _phase = Phase.Intact;
        private float _chainAt;
        private float _burnUntil;
        private bool _chained;
        private BoxCollider[] _boxes = System.Array.Empty<BoxCollider>();

        private void Awake()
        {
            ResolveVisuals();
            _boxes = GetComponentsInChildren<BoxCollider>(true);
            Apply(Phase.Intact);
        }

        private void OnEnable()
        {
            if (!All.Contains(this))
                All.Add(this);
            InteractionPromptBroker.Register(this);
        }

        private void OnDisable()
        {
            All.Remove(this);
            InteractionPromptBroker.Unregister(this);
        }

        private void Update()
        {
            if (_phase == Phase.Burning)
            {
                if (!_chained && Time.time >= _chainAt)
                {
                    _chained = true;
                    LightNeighbors();
                }

                if (Time.time >= _burnUntil)
                    Apply(Phase.Burnt);
                return;
            }

            if (_phase != Phase.Intact) return;
            if (!HasLantern()) return;
            if (Bayou.GameplayPause.BlocksWorldInteract || Bayou.GameplayPause.IsDialoguePlaying)
                return;
            if (SignReadUi.IsOpen) return;

            var closest = ClosestIntact();
            if (closest != this) return;
            if (LosesToCloserInteractable()) return;

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
            if (SignReadUi.IsOpen) return false;
            if (ClosestIntact() != this || LosesToCloserInteractable()) return false;

            prompt = new InteractionPrompt("E", "Set the bramble alight", 62, DistanceSq());
            return true;
        }

        private void Ignite()
        {
            if (_phase != Phase.Intact) return;
            _claimedFrame = Time.frameCount;
            _chained = false;
            _chainAt = Time.time + ChainDelay;
            _burnUntil = Time.time + BurnSeconds;
            Apply(Phase.Burning);
        }

        private void LightNeighbors()
        {
            var reachSq = ChainReach * ChainReach;
            var origin = InteractionPoint();
            for (var i = 0; i < All.Count; i++)
            {
                var other = All[i];
                if (other == null || other == this || other._phase != Phase.Intact) continue;
                var delta = other.transform.position - origin;
                delta.y = 0f;
                if (delta.sqrMagnitude > reachSq) continue;
                other.Ignite();
            }
        }

        private static BrambleBurn ClosestIntact()
        {
            var player = PlayerLocator.Transform;
            if (player == null) return null;
            var reachSq = Reach * Reach;
            BrambleBurn best = null;
            var bestSq = reachSq;
            for (var i = 0; i < All.Count; i++)
            {
                var bramble = All[i];
                if (bramble == null || bramble._phase != Phase.Intact) continue;
                var sq = bramble.DistanceSq();
                if (sq > reachSq || sq > bestSq) continue;
                bestSq = sq;
                best = bramble;
            }

            return best;
        }

        private bool LosesToCloserInteractable()
        {
            var sq = DistanceSq();
            if (ReadableSign.HasWorldInteractAtMost(sq)) return true;
            if (InteractTrigger.HasPendingGateAtMost(sq)) return true;
            if (AltarBurn.HasAltarAtMost(sq)) return true;
            if (BonfireInteractable.HasRestAtMost(sq)) return true;
            // An NPC already in talk range takes E even when this bush is closer.
            if (DialogueTrigger.HasTalkInRange()) return true;
            if (Bayou.Quests.QuestItemPickup.HasPickupAtMost(sq)) return true;
            return false;
        }

        private Vector3 InteractionPoint()
        {
            if (_boxes != null)
            {
                for (var i = 0; i < _boxes.Length; i++)
                {
                    var box = _boxes[i];
                    if (box != null && box.enabled)
                        return box.bounds.center;
                }
            }

            return transform.position;
        }

        private float DistanceSq()
        {
            var player = PlayerLocator.Transform;
            if (player == null) return float.MaxValue;
            var delta = InteractionPoint() - player.position;
            delta.y = 0f;
            return delta.sqrMagnitude;
        }

        private void Apply(Phase phase)
        {
            _phase = phase;
            SetActive(intact, phase == Phase.Intact);
            SetActive(burning, phase == Phase.Burning);
            SetActive(burnt, phase == Phase.Burnt);

            SetFireEffectsActive(phase == Phase.Burning);

            if (phase != Phase.Burnt) return;
            for (var i = 0; i < _boxes.Length; i++)
            {
                if (_boxes[i] != null)
                    _boxes[i].enabled = false;
            }
        }

        private void ResolveVisuals()
        {
            for (var i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                if (child == null) continue;
                var name = child.name;
                if (intact == null && IsState(name, "Normal", "Intact"))
                    intact = child.gameObject;
                else if (burning == null && IsState(name, "Burning"))
                    burning = child.gameObject;
                else if (burnt == null && IsState(name, "Burnt", "Burned"))
                    burnt = child.gameObject;
                else if (_fireEffectsRoot == null && IsFireEffectsName(name))
                    _fireEffectsRoot = child.gameObject;
                else if (fireEffect == null && name == "FireEffect")
                    fireEffect = child.gameObject;
            }

            if (fireEffect == null)
                fireEffect = _fireEffectsRoot;

            CollectFireSystems();
            SetFireEffectsActive(false);
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

            if (fireParticles != null && !_fireSystems.Contains(fireParticles))
                _fireSystems.Add(fireParticles);

            if (_fireEffectsRoot != null)
            {
                var systems = _fireEffectsRoot.GetComponentsInChildren<ParticleSystem>(true);
                for (var i = 0; i < systems.Length; i++)
                {
                    if (systems[i] != null && !_fireSystems.Contains(systems[i]))
                        _fireSystems.Add(systems[i]);
                }
            }

            if (fireEffect != null && fireEffect != _fireEffectsRoot)
            {
                var extra = fireEffect.GetComponentsInChildren<ParticleSystem>(true);
                for (var i = 0; i < extra.Length; i++)
                {
                    if (extra[i] != null && !_fireSystems.Contains(extra[i]))
                        _fireSystems.Add(extra[i]);
                }
            }

            if (fireParticles == null && _fireSystems.Count > 0)
                fireParticles = _fireSystems[0];
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
                    if (!ps.gameObject.activeInHierarchy && _fireEffectsRoot != null)
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

        private static bool HasLantern()
        {
            var inv = InventoryController.Instance;
            if (inv == null) return false;
            return inv.CountExact("Item_Lantern") >= 1 || inv.CountExact("Item_SpiritLantern") >= 1;
        }
    }
}
