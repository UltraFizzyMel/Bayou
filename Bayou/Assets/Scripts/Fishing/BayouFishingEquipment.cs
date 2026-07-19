#if !ENABLE_INPUT_SYSTEM
#error BayouFishingEquipment requires the New Input System (ENABLE_INPUT_SYSTEM).
#endif

using Bayou.Combat;
using Bayou.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Bayou.Fishing
{
    public enum BayouFishingTool
    {
        ThrowingNet,
        HandNet,
        FishingRod
    }

    /// <summary>
    /// Switches between throwing net, hand net, and fishing rod (fish out of combat / melee in combat).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BayouFishingEquipment : MonoBehaviour
    {
        [SerializeField] private FishingNetCaster throwingCaster;
        [SerializeField] private HandNetAreaController handNet;
        [SerializeField] private FishingRodController fishingRod;

        [Header("Input")]
        [Tooltip("Press to cycle Throwing net → Hand net → Fishing rod.")]
        [SerializeField] private InputActionReference switchToolAction;

        [SerializeField] private InputActionReference selectHandNetAction;
        [SerializeField] private InputActionReference selectThrowingNetAction;
        [SerializeField] private InputActionReference selectFishingRodAction;

        [SerializeField] private BayouFishingTool startingTool = BayouFishingTool.FishingRod;

        private InputAction _switchTool;

        public BayouFishingTool CurrentTool { get; private set; }

        private void Reset()
        {
            throwingCaster = GetComponent<FishingNetCaster>();
            handNet = GetComponent<HandNetAreaController>();
            fishingRod = GetComponent<FishingRodController>();
        }

        private void Awake()
        {
            if (throwingCaster == null) throwingCaster = GetComponent<FishingNetCaster>();
            if (handNet == null) handNet = GetComponent<HandNetAreaController>();
            if (fishingRod == null) fishingRod = GetComponent<FishingRodController>();
        }

        private void OnEnable()
        {
            ResolveSwitchAction();
            _switchTool?.Enable();
            selectHandNetAction?.action?.Enable();
            selectThrowingNetAction?.action?.Enable();
            selectFishingRodAction?.action?.Enable();
            ApplyTool(startingTool);
        }

        private void OnDisable()
        {
            selectHandNetAction?.action?.Disable();
            selectThrowingNetAction?.action?.Disable();
            selectFishingRodAction?.action?.Disable();
        }

        private void Update()
        {
            if (selectHandNetAction?.action != null && selectHandNetAction.action.WasPressedThisFrame())
            {
                ApplyTool(BayouFishingTool.HandNet);
                return;
            }

            if (selectThrowingNetAction?.action != null && selectThrowingNetAction.action.WasPressedThisFrame())
            {
                ApplyTool(BayouFishingTool.ThrowingNet);
                return;
            }

            if (selectFishingRodAction?.action != null && selectFishingRodAction.action.WasPressedThisFrame())
            {
                ApplyTool(BayouFishingTool.FishingRod);
                return;
            }

            if (_switchTool != null && _switchTool.WasPressedThisFrame())
                ApplyTool(NextTool(CurrentTool));
        }

        public void ApplyTool(BayouFishingTool tool)
        {
            CurrentTool = tool;

            var useThrow = tool == BayouFishingTool.ThrowingNet || tool == BayouFishingTool.FishingRod;
            var useHand = tool == BayouFishingTool.HandNet;
            var useRod = tool == BayouFishingTool.FishingRod;

            if (throwingCaster != null)
                throwingCaster.enabled = useThrow;

            if (handNet != null)
                handNet.enabled = useHand;

            if (fishingRod != null)
                fishingRod.enabled = useRod;
        }

        private static BayouFishingTool NextTool(BayouFishingTool tool) =>
            tool switch
            {
                BayouFishingTool.ThrowingNet => BayouFishingTool.HandNet,
                BayouFishingTool.HandNet => BayouFishingTool.FishingRod,
                _ => BayouFishingTool.ThrowingNet
            };

        private void ResolveSwitchAction()
        {
            _switchTool = switchToolAction != null ? switchToolAction.action : null;
            if (_switchTool != null) return;

            var motor = GetComponent<BayouCharacterMotor>();
            if (motor?.MoveAction?.action?.actionMap?.asset == null) return;

            _switchTool = motor.MoveAction.action.actionMap.asset
                .FindAction("Player/Switch Tool", throwIfNotFound: false);
        }
    }
}
