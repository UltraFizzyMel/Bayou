using System.Collections.Generic;
using Bayou.Player;
using Bayou.UI;
using UnityEngine;

namespace Bayou.Environment
{
    /// <summary>
    /// Stand near a sign and press Interact (E) to read it.
    /// Range is measured to the sign mesh, so a solid post does not have to be a trigger.
    /// Press E again, or walk away, to close it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ReadableSign : MonoBehaviour, IInteractionPromptSource
    {
        [SerializeField] private string title = "Sign";
        [TextArea(3, 8)]
        [SerializeField] private string body = "The writing is faded.";
        [SerializeField] private string readPrompt = "Read";
        [SerializeField] private float interactRadius = 3.5f;

        private static readonly List<ReadableSign> All = new();

        private bool _playerInRange;
        private float _distSq;

        public void Configure(string signTitle, string signBody, string prompt = "Read")
        {
            if (!string.IsNullOrWhiteSpace(signTitle))
                title = signTitle;
            if (!string.IsNullOrWhiteSpace(signBody))
                body = signBody;
            if (!string.IsNullOrWhiteSpace(prompt))
                readPrompt = prompt;
        }

        private void Reset()
        {
            var col = GetComponent<Collider>();
            if (col != null)
                col.isTrigger = true;
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
            SignReadUi.Hide(this);
        }

        public bool TryGetInteractionPrompt(out InteractionPrompt prompt)
        {
            prompt = default;
            if (!_playerInRange) return false;
            if (SignReadUi.IsOpen && SignReadUi.Owner == this) return false;
            if (Bayou.GameplayPause.BlocksWorldInteract || Bayou.GameplayPause.IsDialoguePlaying)
                return false;

            var action = string.IsNullOrWhiteSpace(readPrompt) ? "Read" : readPrompt;
            prompt = new InteractionPrompt("E", action, 58, DistToPlayerSq());
            return true;
        }

        private void Update()
        {
            RefreshRange();
            if (!_playerInRange || !IsClosestInRange()) return;
            if (Bayou.GameplayPause.BlocksWorldInteract || Bayou.GameplayPause.IsDialoguePlaying)
                return;

            if (!WasInteractPressed())
                return;

            if (SignReadUi.IsOpen && SignReadUi.Owner == this)
            {
                SignReadUi.Hide(this);
                return;
            }

            if (SignReadUi.IsOpen)
                return;

            SignReadUi.Show(this, title, body);
        }

        private void RefreshRange()
        {
            var player = PlayerLocator.Transform;
            if (player == null)
            {
                var motor = UnityEngine.Object.FindFirstObjectByType<BayouCharacterMotor>();
                if (motor != null)
                    player = motor.transform;
            }
            if (player == null)
            {
                _playerInRange = false;
                _distSq = float.MaxValue;
                if (SignReadUi.Owner == this)
                    SignReadUi.Hide(this);
                return;
            }

            var reach = Mathf.Max(1.2f, interactRadius);
            _distSq = DistanceSqToPlayer(player.position);
            var inside = _distSq <= reach * reach;
            if (_playerInRange && !inside && SignReadUi.Owner == this)
                SignReadUi.Hide(this);
            _playerInRange = inside;
        }

        private float DistanceSqToPlayer(Vector3 playerPos)
        {
            var bestSq = FlatSq(transform.position, playerPos);
            var renderers = GetComponentsInChildren<Renderer>();
            for (var i = 0; i < renderers.Length; i++)
            {
                var rend = renderers[i];
                if (rend == null || !rend.enabled) continue;
                if (rend is ParticleSystemRenderer or TrailRenderer) continue;
                var size = rend.bounds.size;
                if (size.x > 30f || size.y > 30f || size.z > 30f) continue;

                var p = rend.bounds.ClosestPoint(playerPos);
                var sq = (p - playerPos).sqrMagnitude;
                if (sq < bestSq)
                    bestSq = sq;
            }

            return bestSq;
        }

        private static float FlatSq(Vector3 a, Vector3 b)
        {
            var d = a - b;
            d.y = 0f;
            return d.sqrMagnitude;
        }

        private bool IsClosestInRange()
        {
            for (var i = 0; i < All.Count; i++)
            {
                if (All[i] != null)
                    All[i].RefreshRange();
            }

            ReadableSign best = null;
            var bestSq = float.MaxValue;
            for (var i = 0; i < All.Count; i++)
            {
                var sign = All[i];
                if (sign == null || !sign._playerInRange) continue;
                if (sign._distSq < bestSq)
                {
                    bestSq = sign._distSq;
                    best = sign;
                }
            }

            return best == this;
        }

        private static bool WasInteractPressed()
        {
#if ENABLE_INPUT_SYSTEM
            var playerInput = UnityEngine.Object.FindFirstObjectByType<UnityEngine.InputSystem.PlayerInput>();
            var interact = playerInput != null ? playerInput.actions?.FindAction("Interact", false) : null;
            if (interact != null && interact.WasPressedThisFrame())
            {
                InputManager.GetInstance()?.RegisterInteractPressed();
                return true;
            }

            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.eKey.wasPressedThisFrame)
            {
                InputManager.GetInstance()?.RegisterInteractPressed();
                return true;
            }
#endif
            var input = InputManager.GetInstance();
            return input != null && input.GetInteractPressed();
        }

        private float DistToPlayerSq() => _distSq;
    }
}
