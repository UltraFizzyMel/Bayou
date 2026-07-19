#if !ENABLE_INPUT_SYSTEM
#error FishingRodController requires the New Input System (ENABLE_INPUT_SYSTEM).
#endif

using Bayou.Fishing;
using Bayou.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Bayou.Combat
{
    /// <summary>
    /// Dual-use fishing rod: Cast fishes when safe; Attack (and Cast while threatened) swings melee.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeleeAttack))]
    public sealed class FishingRodController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private MeleeAttack melee;
        [SerializeField] private BayouCharacterMotor motor;

        [Header("Input")]
        [SerializeField] private InputActionReference attackAction;
        [SerializeField] private InputActionReference castAction;

        [Tooltip("While threatened, Cast also performs a melee swing (fishing is blocked separately).")]
        [SerializeField] private bool castBecomesMeleeInCombat = true;

        private InputAction _attack;
        private InputAction _cast;

        private void Reset()
        {
            melee = GetComponent<MeleeAttack>();
            motor = GetComponent<BayouCharacterMotor>();
        }

        private void Awake()
        {
            if (melee == null) melee = GetComponent<MeleeAttack>();
            if (motor == null) motor = GetComponent<BayouCharacterMotor>();
            melee.SetAttackerTeam(CombatTeam.Player);
            ResolveActions();
        }

        private void OnEnable()
        {
            ResolveActions();
            _attack?.Enable();
            _cast?.Enable();
        }

        private void Update()
        {
            if (!isActiveAndEnabled) return;
            if (FishingActivity.IsBusy) return;

            if (WasPressed(_attack))
            {
                TryMelee();
                return;
            }

            if (castBecomesMeleeInCombat &&
                CombatPresence.IsPlayerThreatened &&
                WasPressed(_cast))
            {
                TryMelee();
            }
        }

        private void TryMelee()
        {
            if (melee == null || melee.IsAttacking) return;
            melee.TryBeginAttack(transform.forward);
        }

        private void ResolveActions()
        {
            _attack = attackAction != null ? attackAction.action : null;
            _cast = castAction != null ? castAction.action : null;

            InputActionAsset asset = null;
            if (motor != null && motor.MoveAction != null && motor.MoveAction.action != null)
                asset = motor.MoveAction.action.actionMap?.asset;

            if (asset != null)
            {
                if (_attack == null)
                    _attack = asset.FindAction("Player/Attack", throwIfNotFound: false);
                if (_cast == null)
                    _cast = asset.FindAction("Player/Cast", throwIfNotFound: false);
            }
        }

        private static bool WasPressed(InputAction action) =>
            action != null && action.WasPressedThisFrame();
    }
}
