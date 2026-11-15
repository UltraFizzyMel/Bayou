using Bayou.Inventory;
using Bayou.UI;
using UnityEngine;

namespace Bayou.World
{
    public enum BurnKind
    {
        Altar = 0,
        Bramble = 1
    }

    /// <summary>
    /// Press E with a lantern to burn an altar or a bramble.
    /// FireEffect starts disabled. Assign a ParticleSystem on this object or on the FireEffect child.
    /// Bramble never writes the altar flags.
    /// </summary>
    [DefaultExecutionOrder(20)]
    [DisallowMultipleComponent]
    public sealed class BurnableInteract : MonoBehaviour, IInteractionPromptSource
    {
        public const string FoggyFlag = "Is_foggyalter_burned";
        public const string GraveFlag = "Is_gravealtar_burned";
        public const string BrackishFlag = "Is_brackishaltar_burned";

        [SerializeField] private BurnKind kind = BurnKind.Altar;
        [SerializeField] private string inkFlag = "";
        [SerializeField] private string brambleFlag = "";
        [SerializeField] private ParticleSystem fireParticles;
        [SerializeField] private GameObject fireEffect;
        [SerializeField] private float interactRadius = 3.4f;

        private bool _burned;

        public bool IsBurned => _burned;
        public string InkFlag => inkFlag ?? "";

        public void Configure(BurnKind burnKind, string flag)
        {
            kind = burnKind;
            if (burnKind == BurnKind.Bramble)
                brambleFlag = flag ?? "";
            else
                inkFlag = flag ?? "";
            EnsureFireSlot();
            _burned = ReadBurned();
            ApplyFire(_burned);
        }

        private void Reset() => EnsureFireSlot();

        private void Awake()
        {
            EnsureFireSlot();
            _burned = ReadBurned();
            ApplyFire(_burned);
        }

        private void OnEnable() => InteractionPromptBroker.Register(this);

        private void OnDisable() => InteractionPromptBroker.Unregister(this);

        private void Update()
        {
            if (_burned || !HasLantern()) return;
            if (Bayou.GameplayPause.BlocksWorldInteract || Bayou.GameplayPause.IsDialoguePlaying)
                return;
            if (!InRange()) return;

            var flower = GetComponent<GraveFlowerPlacement>();
            if (flower != null && flower.ClaimsInteract)
                return;

            var input = InputManager.GetInstance();
            if (input == null || !input.GetInteractPressed())
                return;

            Burn();
        }

        public bool TryGetInteractionPrompt(out InteractionPrompt prompt)
        {
            prompt = default;
            if (_burned || !HasLantern() || !InRange()) return false;
            if (Bayou.GameplayPause.BlocksWorldInteract || Bayou.GameplayPause.IsDialoguePlaying)
                return false;

            var flower = GetComponent<GraveFlowerPlacement>();
            if (flower != null && flower.ClaimsInteract)
                return false;

            var verb = kind == BurnKind.Bramble ? "Set the bramble alight" : "Light the altar";
            prompt = new InteractionPrompt("E", verb, 60, DistanceSq());
            return true;
        }

        public void Burn()
        {
            if (_burned) return;
            _burned = true;
            if (!string.IsNullOrWhiteSpace(inkFlag))
            {
                WorldFlags.Set(inkFlag, true);
                DialogueManager.GetInstance()?.SetBoolVariable(inkFlag, true);
            }

            if (kind == BurnKind.Bramble && !string.IsNullOrWhiteSpace(brambleFlag))
            {
                WorldFlags.Set(brambleFlag, true);
                DialogueManager.GetInstance()?.SetBoolVariable(brambleFlag, true);
            }

            ApplyFire(true);
            TryAdvanceSeals();
        }

        private void TryAdvanceSeals()
        {
            if (kind != BurnKind.Altar) return;
            if (!WorldFlags.Has(FoggyFlag) || !WorldFlags.Has(GraveFlag) || !WorldFlags.Has(BrackishFlag))
                return;

            QuestManager.Resolve()?.MarkReadyToFinish(QuestIds.BreakTheSeals);
        }

        private bool ReadBurned()
        {
            if (kind == BurnKind.Bramble)
            {
                if (!string.IsNullOrWhiteSpace(brambleFlag) && WorldFlags.Has(brambleFlag))
                    return true;
                return false;
            }

            return !string.IsNullOrWhiteSpace(inkFlag) && WorldFlags.Has(inkFlag);
        }

        private void EnsureFireSlot()
        {
            if (fireEffect == null)
            {
                var existing = transform.Find("FireEffect");
                if (existing == null)
                {
                    var go = new GameObject("FireEffect");
                    go.transform.SetParent(transform, false);
                    go.transform.localPosition = new Vector3(0f, 1.25f, 0f);
                    existing = go.transform;
                }

                fireEffect = existing.gameObject;
            }

            if (fireParticles == null)
                fireParticles = fireEffect.GetComponent<ParticleSystem>() ??
                                fireEffect.GetComponentInChildren<ParticleSystem>(true);
        }

        private void ApplyFire(bool burning)
        {
            EnsureFireSlot();
            if (fireEffect != null && fireEffect.activeSelf != burning)
                fireEffect.SetActive(burning);

            if (fireParticles == null) return;
            if (burning)
            {
                if (!fireParticles.gameObject.activeSelf)
                    fireParticles.gameObject.SetActive(true);
                fireParticles.Play(true);
            }
            else
            {
                fireParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        private static bool HasLantern()
        {
            var inv = InventoryController.Instance;
            if (inv == null) return false;
            return inv.HasItemsById("Item_Lantern", 1) || inv.HasItemsById("Item_SpiritLantern", 1);
        }

        private bool InRange()
        {
            var player = Bayou.Player.PlayerLocator.Transform;
            if (player == null) return false;
            var reach = Mathf.Max(1.5f, interactRadius);
            return DistanceSq() <= reach * reach;
        }

        private float DistanceSq()
        {
            var player = Bayou.Player.PlayerLocator.Transform;
            if (player == null) return float.MaxValue;
            var d = player.position - transform.position;
            d.y *= 0.35f;
            return d.sqrMagnitude;
        }
    }
}
