using System.Collections.Generic;
using Bayou.Environment;
using Bayou.Player;
using Bayou.Quests;
using Bayou.Save;
using Bayou.UI;
using Bayou.World;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Talk range is the trigger volume, plus a short step in front of it.
/// This runs before bramble, signs, and pickups so an in-range NPC gets E
/// unless a sign, gate, altar, campfire, or pickup is strictly closer.
/// </summary>
[DefaultExecutionOrder(-100)]
public class DialogueTrigger : MonoBehaviour, IInteractionPromptSource
{
    private const float DoorstepMargin = 0.9f;

    private static readonly List<DialogueTrigger> Active = new();
    [Header("Visual Cue")]
    [SerializeField] private GameObject buttonCue;
    [SerializeField] private GameObject NPCIndicator;

    [Header("Ink JSON")]
    [SerializeField] private TextAsset inkJSON;
    [SerializeField] private string knotName = "";
    [SerializeField] private string talkPrompt = "Talk";

    [SerializeField] private bool playerInRange;

    private Collider _volume;
    private static PlayerInput _playerInput;

    private void Awake()
    {
        playerInRange = false;
        _volume = GetComponent<Collider>();
        if (buttonCue != null)
            buttonCue.SetActive(false);
        if (NPCIndicator != null)
            NPCIndicator.SetActive(true);
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

    /// <summary>A quest NPC already in talk range, at least as close as <paramref name="distSq"/>.</summary>
    public static bool HasTalkAtMost(float distSq)
    {
        for (var i = 0; i < Active.Count; i++)
        {
            var npc = Active[i];
            if (npc == null || !npc.isActiveAndEnabled || !npc.playerInRange) continue;
            if (npc.DistToPlayerSq() <= distSq)
                return true;
        }

        return false;
    }

    /// <summary>True when the player is already inside some NPC's talk range.</summary>
    public static bool HasTalkInRange()
    {
        for (var i = 0; i < Active.Count; i++)
        {
            var npc = Active[i];
            if (npc != null && npc.isActiveAndEnabled && npc.playerInRange)
                return true;
        }

        return false;
    }

    private void Update()
    {
        RefreshRange();

        var dialogue = DialogueManager.GetInstance();
        var canTalk = playerInRange && dialogue != null && !dialogue.dialogueIsPlaying && !ShopIsOpen() &&
                      !Bayou.GameplayPause.BlocksWorldInteract && IsClosestTalk();

        if (buttonCue != null)
            buttonCue.SetActive(canTalk && !LosesToCloserInteractable());
        if (NPCIndicator != null)
            NPCIndicator.SetActive(!canTalk || LosesToCloserInteractable());

        if (!canTalk || LosesToCloserInteractable() || inkJSON == null) return;
        if (!InteractWasPressed()) return;

        dialogue.EnterDialogueMode(inkJSON, knotName);
    }

    public bool TryGetInteractionPrompt(out InteractionPrompt prompt)
    {
        prompt = default;
        var dialogue = DialogueManager.GetInstance();
        if (!playerInRange || dialogue == null || dialogue.dialogueIsPlaying || ShopIsOpen() ||
            Bayou.GameplayPause.BlocksWorldInteract || !IsClosestTalk() || LosesToCloserInteractable())
            return false;

        prompt = new InteractionPrompt("E", talkPrompt, 60, DistToPlayerSq());
        return true;
    }

    private bool IsClosestTalk()
    {
        DialogueTrigger best = null;
        var bestSq = float.MaxValue;
        for (var i = 0; i < Active.Count; i++)
        {
            var npc = Active[i];
            if (npc == null || !npc.isActiveAndEnabled || !npc.playerInRange) continue;
            var sq = npc.DistToPlayerSq();
            if (sq < bestSq)
            {
                bestSq = sq;
                best = npc;
            }
        }

        return best == this;
    }

    /// <summary>A strictly closer sign, gate, altar, campfire, or pickup keeps E.</summary>
    private bool LosesToCloserInteractable()
    {
        var closer = Mathf.Max(0f, DistToPlayerSq() - 0.05f);
        if (ReadableSign.HasWorldInteractAtMost(closer)) return true;
        if (InteractTrigger.HasPendingGateAtMost(closer)) return true;
        if (BurnableInteract.HasAltarAtMost(closer)) return true;
        if (BonfireInteractable.HasRestAtMost(closer)) return true;
        if (QuestItemPickup.HasPickupAtMost(closer)) return true;
        return false;
    }

    private static bool ShopIsOpen()
    {
        var shop = Bayou.Inventory.Shop.ShopUIController.ActiveShop;
        return shop != null && shop.IsOpen;
    }

    private float DistToPlayerSq()
    {
        var p = PlayerLocator.Transform;
        if (p == null) return 0f;
        var d = transform.position - p.position;
        d.y = 0f;
        return d.sqrMagnitude;
    }

    private void RefreshRange()
    {
        var player = PlayerLocator.Transform;
        if (player == null)
        {
            playerInRange = false;
            return;
        }

        if (VolumeContains(player))
        {
            playerInRange = true;
            return;
        }

        var flat = player.position - transform.position;
        flat.y = 0f;
        if (Mathf.Abs(player.position.y - transform.position.y) > 3.2f)
        {
            playerInRange = false;
            return;
        }

        playerInRange = flat.sqrMagnitude <= TalkReach() * TalkReach();
    }

    private float TalkReach()
    {
        var reach = DoorstepMargin;
        if (_volume is BoxCollider box)
        {
            var ext = box.size * 0.5f;
            var lossy = transform.lossyScale;
            var hx = Mathf.Abs(ext.x * lossy.x);
            var hz = Mathf.Abs(ext.z * lossy.z);
            reach += Mathf.Max(hx, hz);
        }
        else if (_volume != null)
        {
            var ext = _volume.bounds.extents;
            reach += Mathf.Max(ext.x, ext.z);
        }
        else
        {
            reach += 2.2f;
        }

        return reach;
    }

    private bool VolumeContains(Transform player)
    {
        if (_volume == null || !_volume.enabled) return false;

        var origin = player.position;
        var capsule = player.GetComponent<CapsuleCollider>();
        var height = capsule != null ? capsule.height : 2f;
        var center = capsule != null ? capsule.center.y : 1f;
        if (Inside(origin)) return true;
        if (Inside(origin + Vector3.up * Mathf.Max(0.15f, center - height * 0.5f))) return true;
        if (Inside(origin + Vector3.up * center)) return true;
        return Inside(origin + Vector3.up * (center + height * 0.35f));

        bool Inside(Vector3 point)
        {
            var closest = _volume.ClosestPoint(point);
            return (closest - point).sqrMagnitude <= 0.04f;
        }
    }

    private static bool InteractWasPressed()
    {
        var kb = Keyboard.current;
        if (kb != null && kb.eKey.wasPressedThisFrame)
        {
            InputManager.GetInstance()?.RegisterInteractPressed();
            return true;
        }

        if (_playerInput == null)
            _playerInput = Object.FindFirstObjectByType<PlayerInput>();
        var interact = _playerInput != null ? _playerInput.actions?.FindAction("Interact", false) : null;
        if (interact != null && interact.WasPressedThisFrame())
        {
            InputManager.GetInstance()?.RegisterInteractPressed();
            return true;
        }

        var input = InputManager.GetInstance();
        return input != null && input.GetInteractPressed();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (IsPlayer(other))
            playerInRange = true;
    }

    private void OnTriggerStay(Collider other)
    {
        if (IsPlayer(other))
            playerInRange = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (IsPlayer(other))
            RefreshRange();
    }

    private static bool IsPlayer(Collider other)
    {
        return other != null &&
               (other.TryGetComponent<BayouCharacterMotor>(out _) ||
                other.GetComponentInParent<BayouCharacterMotor>() != null);
    }
}
