using UnityEngine;

namespace Bayou.Fishing
{
    /// <summary>
    /// Lightweight playtest HUD for fishing phases (attract / reel / cancel).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FishingHud : MonoBehaviour
    {
        private FishingNetCaster _caster;
        private BayouFishingEquipment _equipment;
        private GUIStyle _boxStyle;
        private GUIStyle _labelStyle;

        private void Awake()
        {
            _caster = GetComponent<FishingNetCaster>() ?? FindFirstObjectByType<FishingNetCaster>();
            _equipment = GetComponent<BayouFishingEquipment>() ?? FindFirstObjectByType<BayouFishingEquipment>();
        }

        private void OnGUI()
        {
            if (!Application.isPlaying) return;

            EnsureStyles();

            var attract = FindActiveAttract();
            var reel = FindActiveReel();
            if (_caster == null && attract == null && reel == null)
                return;

            var casting = _caster != null && _caster.Phase != FishingCastPhase.Idle;
            var hasNet = _caster != null && _caster.HasActiveNet;
            if (!casting && !hasNet && attract == null && reel == null)
                return;

            var area = new Rect(16f, Screen.height - 140f, 420f, 120f);
            GUI.Box(area, GUIContent.none, _boxStyle);
            GUILayout.BeginArea(new Rect(area.x + 12f, area.y + 10f, area.width - 24f, area.height - 16f));

            if (_equipment != null)
                GUILayout.Label($"Tool: {_equipment.CurrentTool}  (Tab / 1=Rod / 2=Net)", _labelStyle);

            if (reel != null && reel.IsActive)
            {
                GUILayout.Label("REEL — hold LMB / Cast", _labelStyle);
                DrawBar(reel.Progress01);
                GUILayout.Label("Esc / Q / RMB cancel", _labelStyle);
            }
            else if (attract != null && attract.IsActive)
            {
                GUILayout.Label("NET PLANTED — fish swimming in", _labelStyle);
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
                GUILayout.Label("POWER — hold LMB, release to cast", _labelStyle);
                DrawBar(_caster.CurrentCharge01);
                GUILayout.Label("Esc / Q / RMB cancel", _labelStyle);
            }
            else if (hasNet)
            {
                GUILayout.Label("Net in flight…", _labelStyle);
                GUILayout.Label("Esc / Q / RMB cancel", _labelStyle);
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
            foreach (var a in FindObjectsByType<FishingAttractPhase>(FindObjectsSortMode.None))
            {
                if (a != null && a.IsActive) return a;
            }

            return null;
        }

        private static FishingReelPhase FindActiveReel()
        {
            foreach (var r in FindObjectsByType<FishingReelPhase>(FindObjectsSortMode.None))
            {
                if (r != null && r.IsActive) return r;
            }

            return null;
        }
    }
}
