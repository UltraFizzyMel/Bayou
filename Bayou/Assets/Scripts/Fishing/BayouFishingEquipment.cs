#if !ENABLE_INPUT_SYSTEM
#error BayouFishingEquipment requires the New Input System (ENABLE_INPUT_SYSTEM).
#endif

using UnityEngine;
using UnityEngine.InputSystem;

namespace Bayou.Fishing
{
    public enum BayouFishingTool
    {
        ThrowingNet,
        HandNet
    }

    /// <summary>
    /// Switches between long-range throwing net (<see cref="FishingNetCaster"/>) and short-range area hand net.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BayouFishingEquipment : MonoBehaviour
    {
        [SerializeField] private FishingNetCaster throwingCaster;
        [SerializeField] private HandNetAreaController handNet;

        [Header("Input")]
        [Tooltip("Press to cycle Throwing net ↔ Hand net (or wire Prev/Next separately).")]
        [SerializeField] private InputActionReference switchToolAction;

        [Tooltip("Optional: switch directly to hand net.")]
        [SerializeField] private InputActionReference selectHandNetAction;

        [Tooltip("Optional: switch directly to throwing net.")]
        [SerializeField] private InputActionReference selectThrowingNetAction;

        [SerializeField] private BayouFishingTool startingTool = BayouFishingTool.ThrowingNet;

        public BayouFishingTool CurrentTool { get; private set; }

        private void Reset()
        {
            throwingCaster = GetComponent<FishingNetCaster>();
            handNet = GetComponent<HandNetAreaController>();
        }

        private void Awake()
        {
            if (throwingCaster == null) throwingCaster = GetComponent<FishingNetCaster>();
            if (handNet == null) handNet = GetComponent<HandNetAreaController>();
        }

        private void OnEnable()
        {
            switchToolAction?.action?.Enable();
            selectHandNetAction?.action?.Enable();
            selectThrowingNetAction?.action?.Enable();
            ApplyTool(startingTool);
        }

        private void OnDisable()
        {
            switchToolAction?.action?.Disable();
            selectHandNetAction?.action?.Disable();
            selectThrowingNetAction?.action?.Disable();
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

            if (switchToolAction?.action != null && switchToolAction.action.WasPressedThisFrame())
                ApplyTool(CurrentTool == BayouFishingTool.ThrowingNet ? BayouFishingTool.HandNet : BayouFishingTool.ThrowingNet);
        }

        public void ApplyTool(BayouFishingTool tool)
        {
            CurrentTool = tool;

            if (throwingCaster != null)
                throwingCaster.enabled = tool == BayouFishingTool.ThrowingNet;

            if (handNet != null)
                handNet.enabled = tool == BayouFishingTool.HandNet;
        }
    }
}
