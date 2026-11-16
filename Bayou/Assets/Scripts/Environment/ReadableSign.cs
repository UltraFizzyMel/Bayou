using System.Collections.Generic;
using Bayou.Inventory;
using Bayou.Player;
using Bayou.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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
        private static int _rangeFrame = -1;

        private bool _playerInRange;
        private float _distSq;
        private Renderer[] _renderers = System.Array.Empty<Renderer>();

        /// <summary>A sign the player can already read, at least as close as <paramref name="distSq"/>, or a sign panel that is open.</summary>
        public static bool HasWorldInteractAtMost(float distSq)
        {
            if (SignReadUi.IsOpen) return true;
            for (var i = 0; i < All.Count; i++)
            {
                var sign = All[i];
                if (sign == null || !sign._playerInRange) continue;
                if (sign._distSq <= distSq)
                    return true;
            }

            return false;
        }

        private void Start() => RefreshLiveText();

        /// <summary>
        /// The read panel uses the fields on this component. A placeholder does not replace
        /// a sentence already written on the sign.
        /// </summary>
        public void RefreshLiveText()
        {
            var label = ReadChildLabel();
            if (IsPlaceholder(title, body) && !string.IsNullOrWhiteSpace(label) && !IsPlaceholder(title, label))
            {
                var split = label.IndexOf('\n');
                if (split > 0)
                {
                    title = label.Substring(0, split).Trim();
                    body = label.Substring(split + 1).Trim();
                }
                else
                {
                    body = label.Trim();
                }
            }
            else if (!IsPlaceholder(title, body))
            {
                WriteChildLabelIfBlank();
            }

            if (SignReadUi.IsOpen && SignReadUi.Owner == this)
                SignReadUi.Show(this, title, body);
        }

        private static bool IsPlaceholder(string signTitle, string signBody)
        {
            if (string.IsNullOrWhiteSpace(signBody)) return true;
            var bodyTrim = signBody.Trim();
            if (bodyTrim == "..." || bodyTrim == "The writing is faded.") return true;
            return signTitle == "Sign" ||
                   signTitle == "Church road" ||
                   signTitle == "Foggy marsh" ||
                   signTitle == "Graveyard path" ||
                   signTitle == "Mile marker";
        }

        private string ReadChildLabel()
        {
            var tmp = GetComponentsInChildren<TMP_Text>(true);
            for (var i = 0; i < tmp.Length; i++)
            {
                if (tmp[i] == null) continue;
                var text = tmp[i].text;
                if (!string.IsNullOrWhiteSpace(text))
                    return text;
            }

            var ui = GetComponentsInChildren<Text>(true);
            for (var i = 0; i < ui.Length; i++)
            {
                if (ui[i] == null) continue;
                if (!string.IsNullOrWhiteSpace(ui[i].text))
                    return ui[i].text;
            }

            return null;
        }

        private void WriteChildLabelIfBlank()
        {
            var next = string.IsNullOrWhiteSpace(title) ? body : title + "\n" + body;
            var tmp = GetComponentsInChildren<TMP_Text>(true);
            for (var i = 0; i < tmp.Length; i++)
            {
                if (tmp[i] == null || !IsPlaceholder(title, tmp[i].text)) continue;
                tmp[i].text = next;
            }
        }

        public void Configure(string signTitle, string signBody, string prompt = "Read")
        {
            if (!string.IsNullOrWhiteSpace(signTitle))
                title = signTitle;
            if (!string.IsNullOrWhiteSpace(signBody))
                body = signBody;
            if (!string.IsNullOrWhiteSpace(prompt))
                readPrompt = prompt;
        }

        private void OnEnable()
        {
            if (!All.Contains(this))
                All.Add(this);
            _renderers = GetComponentsInChildren<Renderer>(true);
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
            RefreshAllRangesOnce();
            if (!_playerInRange || !IsClosestInRange()) return;
            if (Bayou.GameplayPause.BlocksWorldInteract || Bayou.GameplayPause.IsDialoguePlaying)
                return;
            if (InventoryDisplayUI.Active != null && InventoryDisplayUI.Active.IsOpen)
                return;
            if (Bayou.World.BrambleBurn.TookInteractThisFrame)
                return;
            if (DialogueTrigger.HasTalkAtMost(_distSq))
                return;
            if (Bayou.Quests.QuestItemPickup.HasPickupAtMost(_distSq))
                return;
            if (SignReadUi.IsOpen && SignReadUi.Owner != this)
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
            if (string.Equals(title, "Zenon Landry", System.StringComparison.OrdinalIgnoreCase))
                WorldFlags.MarkSeenZenonGrave();
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
            var renderers = _renderers;
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

        private static void RefreshAllRangesOnce()
        {
            if (_rangeFrame == Time.frameCount) return;
            _rangeFrame = Time.frameCount;
            for (var i = 0; i < All.Count; i++)
                All[i]?.RefreshRange();
        }

        private bool IsClosestInRange()
        {
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
            var playerInput = CachedPlayerInput();
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

        private static UnityEngine.InputSystem.PlayerInput _playerInput;

        private static UnityEngine.InputSystem.PlayerInput CachedPlayerInput()
        {
            if (_playerInput == null)
                _playerInput = UnityEngine.Object.FindFirstObjectByType<UnityEngine.InputSystem.PlayerInput>();
            return _playerInput;
        }

        private float DistToPlayerSq() => _distSq;
    }
}
