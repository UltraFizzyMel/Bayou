using UnityEngine;

namespace Bayou.Fishing
{
    /// <summary>
    /// Lightweight playtest HUD for fishing phases (attract / reel / cancel).
    /// Always shows which tool is held so rod casting is not a mystery.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FishingHud : MonoBehaviour
    {
        private FishingNetCaster _caster;
        private BayouFishingEquipment _equipment;
        private HandNetAreaController _handNet;
        private GUIStyle _boxStyle;
        private GUIStyle _labelStyle;

        private void Awake()
        {
            _caster = GetComponent<FishingNetCaster>() ?? FindFirstObjectByType<FishingNetCaster>();
            _equipment = GetComponent<BayouFishingEquipment>() ?? FindFirstObjectByType<BayouFishingEquipment>();
            _handNet = GetComponent<HandNetAreaController>() ?? FindFirstObjectByType<HandNetAreaController>();
        }

        private void OnGUI()
        {
            if (!Application.isPlaying) return;

            var attract = FindActiveAttract();
            var reel = FindActiveReel();
            var casting = _caster != null && _caster.Phase != FishingCastPhase.Idle;
            var hasNet = _caster != null && _caster.HasActiveNet;
            var netCharging = _handNet != null && _handNet.IsCharging;
            var melee = _caster != null && _caster.IsMeleeMode ||
                        _equipment != null && _equipment.NetMode == HandNetMode.Combat;
            if (!casting && !hasNet && attract == null && reel == null && !netCharging && !melee)
                return;

            EnsureStyles();

            // Compact tip while idle; taller box during cast phases.
            var height = casting || hasNet || attract != null || reel != null || netCharging ? 128f : 64f;
            var area = new Rect(16f, Screen.height - height - 16f, 460f, height);
            GUI.Box(area, GUIContent.none, _boxStyle);
            GUILayout.BeginArea(new Rect(area.x + 12f, area.y + 10f, area.width - 24f, area.height - 16f));

            if (reel != null && reel.IsActive)
            {
                GUILayout.Label("REEL — hold LMB / Cast", _labelStyle);
                DrawBar(reel.Progress01);
                GUILayout.Label("Esc / Q / RMB cancel", _labelStyle);
            }
            else if (attract != null && attract.IsActive)
            {
                GUILayout.Label("BOBBER DOWN — fish swimming in. Bite when the bar fills.", _labelStyle);
                DrawBar(attract.Progress01);
                GUILayout.Label("Wiggle A/D to attract  |  Esc cancel", _labelStyle);
            }
            else if (_caster != null && _caster.Phase == FishingCastPhase.DirectionSweep)
            {
                GUILayout.Label("AIM — hold LMB to lock, or Space", _labelStyle);
                GUILayout.Label("Esc / Q / RMB cancel", _labelStyle);
            }
            else if (_caster != null && _caster.Phase == FishingCastPhase.ChargingTrajectory)
            {
                GUILayout.Label("POWER — face a direction, hold LMB, release to cast", _labelStyle);
                DrawBar(_caster.CurrentCharge01);
                GUILayout.Label("8-way facing · Esc / Q / RMB cancel", _labelStyle);
            }
            else if (hasNet && _caster != null)
            {
                var net = FishingNetProjectile.Current ?? FishingNetProjectile.ActiveInWater;
                var hint = net != null && !string.IsNullOrEmpty(net.StatusHint)
                    ? net.StatusHint
                    : "Line out — Esc / Q / RMB recast";
                GUILayout.Label(hint, _labelStyle);
                GUILayout.Label("Esc / Q / RMB cancel", _labelStyle);
            }
            else if (_equipment != null && _equipment.CurrentItem == BayouHeldItem.Rod)
            {
                if (_caster != null && _caster.IsMeleeMode)
                    GUILayout.Label("ROD MELEE — LMB swing", _labelStyle);
                else
                {
                    var spot = FishingSpot.FindContaining(_equipment.transform.position);
                    if (spot != null && spot.RequiredTool == FishCatchTool.Rod)
                        GUILayout.Label("Rod hole — hold LMB, release to cast. Wiggle A/D after it lands.", _labelStyle);
                    else if (spot != null && spot.RequiredTool == FishCatchTool.Net)
                        GUILayout.Label("This hole needs the NET (2), not the rod.", _labelStyle);
                    else
                        GUILayout.Label("Hold LMB · release to cast. Look for a rod hole (catfish).", _labelStyle);
                }
            }
            else if (_equipment != null && _equipment.CurrentItem == BayouHeldItem.Net)
            {
                if (_equipment.NetMode == HandNetMode.Combat)
                    GUILayout.Label("COMBAT — LMB swing net (catch snake / stun croc)", _labelStyle);
                else
                {
                    if (_handNet != null && _handNet.IsCharging)
                    {
                        GUILayout.Label("THROW — release at the BIG circle", _labelStyle);
                        DrawBar(_handNet.Pulse01);
                        GUILayout.Label("RMB / Esc cancel  ·  small circle = miss", _labelStyle);
                    }
                    else
                    {
                        GUILayout.Label("NET — hold LMB, release at the big pulse to throw", _labelStyle);
                    }
                }
            }
            else if (_equipment != null && _equipment.IsPursued)
            {
                GUILayout.Label("Pursued! 1 rod melee · 2 net melee", _labelStyle);
            }

            GUILayout.EndArea();
        }

        private void DrawBar(float t01)
        {
            var r = GUILayoutUtility.GetRect(18f, 18f, GUILayout.ExpandWidth(true));
            UnityEngine.GUI.Box(r, GUIContent.none);
            var fill = new Rect(r.x + 2f, r.y + 2f, (r.width - 4f) * Mathf.Clamp01(t01), r.height - 4f);
            var old = UnityEngine.GUI.color;
            UnityEngine.GUI.color = new Color(0.35f, 0.85f, 0.55f, 1f);
            UnityEngine.GUI.DrawTexture(fill, Texture2D.whiteTexture);
            UnityEngine.GUI.color = old;
        }

        private void EnsureStyles()
        {
            if (_labelStyle != null) return;
            _labelStyle = new GUIStyle(UnityEngine.GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            _boxStyle = new GUIStyle(UnityEngine.GUI.skin.box);
        }

        private static FishingAttractPhase FindActiveAttract()
        {
            var a = FishingAttractPhase.Active;
            return a != null && a.IsActive ? a : null;
        }

        private static FishingReelPhase FindActiveReel()
        {
            var r = FishingReelPhase.Active;
            return r != null && r.IsActive ? r : null;
        }
    }
}
