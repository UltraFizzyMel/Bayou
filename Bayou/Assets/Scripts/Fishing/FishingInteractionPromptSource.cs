using Bayou.Fishing;
using Bayou.Inventory;
using Bayou.UI;
using UnityEngine;

namespace Bayou.Fishing
{
    /// <summary>
    /// Contextual prompts for rod cast / hand-net scoop / attract / reel based on held tool + phase.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FishingInteractionPromptSource : MonoBehaviour, IInteractionPromptSource
    {
        [SerializeField] private BayouFishingEquipment equipment;
        [SerializeField] private FishingNetCaster rodCaster;
        [SerializeField] private HandNetAreaController handNet;

        private void Reset()
        {
            equipment = GetComponent<BayouFishingEquipment>();
            rodCaster = GetComponent<FishingNetCaster>();
            handNet = GetComponent<HandNetAreaController>();
        }

        private void Awake()
        {
            if (equipment == null) equipment = GetComponent<BayouFishingEquipment>();
            if (rodCaster == null) rodCaster = GetComponent<FishingNetCaster>();
            if (handNet == null) handNet = GetComponent<HandNetAreaController>();
        }

        private void OnEnable() => InteractionPromptBroker.Register(this);
        private void OnDisable() => InteractionPromptBroker.Unregister(this);

        public bool TryGetInteractionPrompt(out InteractionPrompt prompt)
        {
            prompt = default;

            var bag = InventoryDisplayUI.Active;
            if (bag != null && bag.IsOpen)
                return false;

            // Active reel / attract on planted net — highest fishing priority.
            var reel = FindActiveReel();
            if (reel != null && reel.IsActive)
            {
                prompt = new InteractionPrompt("Hold LMB", "Reel in", 100);
                return true;
            }

            var attract = FindActiveAttract();
            if (attract != null && attract.IsActive)
            {
                prompt = new InteractionPrompt("A / D", "Wiggle — wait for the bite, then hold LMB", 95);
                return true;
            }

            if (rodCaster != null && rodCaster.enabled)
            {
                if (rodCaster.IsMeleeMode)
                {
                    prompt = new InteractionPrompt("LMB", "Swing rod", 70);
                    return true;
                }

                if (rodCaster.Phase == FishingCastPhase.DirectionSweep)
                {
                    prompt = new InteractionPrompt("LMB", "Lock aim", 90);
                    return true;
                }

                if (rodCaster.Phase == FishingCastPhase.ChargingTrajectory)
                {
                    prompt = new InteractionPrompt("Hold LMB", "Charge · release to cast", 90);
                    return true;
                }

                if (rodCaster.HasActiveNet)
                {
                    var net = FishingNetProjectile.Current ?? FishingNetProjectile.ActiveInWater;
                    var action = net != null && !string.IsNullOrEmpty(net.StatusHint)
                        ? net.StatusHint
                        : "Cancel cast";
                    prompt = new InteractionPrompt("Esc", action, 85);
                    return true;
                }

                if (equipment != null && equipment.CurrentItem == BayouHeldItem.Rod)
                {
                    var spot = FishingSpot.FindNearby(transform.position);
                    if (spot == null)
                        return false;
                    if (spot.RequiredTool == FishCatchTool.Net)
                        prompt = new InteractionPrompt("2", "This hole needs the net", 45);
                    else
                        prompt = new InteractionPrompt("Hold LMB", "Cast into this rod hole", 50);
                    return true;
                }
            }

            if (handNet != null && handNet.enabled &&
                equipment != null && equipment.CurrentItem == BayouHeldItem.Net)
            {
                if (handNet.IsCombatMode)
                {
                    prompt = new InteractionPrompt("LMB", "Swing net", 40);
                    return true;
                }

                if (handNet.IsCharging)
                {
                    prompt = new InteractionPrompt("Release", "Throw at the big circle", 90);
                    return true;
                }

                if (FishingSpot.FindNearby(transform.position) == null)
                    return false;

                prompt = new InteractionPrompt("Hold LMB", "Wind up throw", 40);
                return true;
            }

            return false;
        }

        private static FishingAttractPhase FindActiveAttract()
        {
            var all = Object.FindObjectsByType<FishingAttractPhase>(FindObjectsSortMode.None);
            for (var i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].IsActive)
                    return all[i];
            }

            return null;
        }

        private static FishingReelPhase FindActiveReel()
        {
            var all = Object.FindObjectsByType<FishingReelPhase>(FindObjectsSortMode.None);
            for (var i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].IsActive)
                    return all[i];
            }

            return null;
        }
    }
}
