using System.Collections.Generic;
using Bayou.CameraControl;
using Bayou.Creatures;
using Bayou.Rendering;
using UnityEngine;

namespace Bayou.Combat
{
    /// <summary>Hit sparks, floating numbers, and a short camera punch for melee.</summary>
    public sealed class CombatFeedback : MonoBehaviour
    {
        private const int MaxSparks = 12;
        private const int MaxPopups = 12;

        private static CombatFeedback _instance;
        private readonly List<Spark> _sparks = new(MaxSparks);
        private readonly List<Popup> _popups = new(MaxPopups);
        private GUIStyle _popupStyle;

        public static CombatFeedback Resolve()
        {
            if (_instance != null) return _instance;
            var go = new GameObject("CombatFeedback");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<CombatFeedback>();
            return _instance;
        }

        public static void PlayHit(Vector3 worldPos, NetHitResult result, Vector3 awayFromAttacker)
        {
            var fx = Resolve();
            fx.SpawnSpark(worldPos + Vector3.up * 0.55f, awayFromAttacker);
            switch (result)
            {
                case NetHitResult.Killed:
                    fx.SpawnPopup(worldPos, "DOWN", new Color(1f, 0.45f, 0.2f, 1f));
                    break;
                case NetHitResult.Damaged:
                    fx.SpawnPopup(worldPos, "HIT", new Color(1f, 0.85f, 0.35f, 1f));
                    break;
                case NetHitResult.Stunned:
                    fx.SpawnPopup(worldPos, "STUN", new Color(0.55f, 0.85f, 1f, 1f));
                    break;
                case NetHitResult.Caught:
                    fx.SpawnPopup(worldPos, "CAUGHT", new Color(0.45f, 1f, 0.55f, 1f));
                    break;
            }

            PunchCamera(awayFromAttacker);
        }

        public static void PlayWhiff(Vector3 origin, Vector3 forward)
        {
            Resolve().SpawnPopup(origin + forward * 1.2f, "—", new Color(0.85f, 0.85f, 0.85f, 0.7f));
        }

        public static void PlayPlayerHurt(Vector3 worldPos, Vector3 knockback)
        {
            var fx = Resolve();
            fx.SpawnSpark(worldPos + Vector3.up * 0.85f, knockback);
            fx.SpawnPopup(worldPos, "!", new Color(1f, 0.28f, 0.22f, 1f));
            PunchCamera(knockback.sqrMagnitude > 0.0001f ? knockback : Vector3.back);
        }

        public static void PunchCamera(Vector3 worldDir)
        {
            var cam = Object.FindFirstObjectByType<BayouFollowCamera>();
            if (cam == null) return;
            worldDir.y = 0f;
            if (worldDir.sqrMagnitude < 0.0001f)
                worldDir = Vector3.back;
            cam.Punch(-worldDir.normalized * 0.28f);
        }

        private void SpawnSpark(Vector3 pos, Vector3 away)
        {
            away.y = 0f;
            if (away.sqrMagnitude < 0.0001f) away = Vector3.right;
            away.Normalize();

            var go = new GameObject("HitSpark");
            go.transform.position = pos;
            var lr = go.AddComponent<LineRenderer>();
            lr.loop = false;
            lr.useWorldSpace = true;
            lr.positionCount = 2;
            lr.startWidth = 0.11f;
            lr.endWidth = 0.02f;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            var color = new Color(1f, 0.92f, 0.55f, 1f);
            lr.material = BayouShaderUtil.CreateUnlitColor(color);
            lr.startColor = color;
            lr.endColor = new Color(color.r, color.g, color.b, 0f);
            lr.SetPosition(0, pos);
            lr.SetPosition(1, pos + (away + Vector3.up * 0.35f).normalized * 0.35f);

            var extra = new LineRenderer[2];
            for (var i = 0; i < extra.Length; i++)
            {
                var child = new GameObject("SparkArm");
                child.transform.SetParent(go.transform, false);
                var arm = child.AddComponent<LineRenderer>();
                arm.useWorldSpace = true;
                arm.positionCount = 2;
                arm.startWidth = 0.07f;
                arm.endWidth = 0.015f;
                arm.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                arm.receiveShadows = false;
                arm.material = BayouShaderUtil.CreateUnlitColor(color);
                arm.startColor = color;
                arm.endColor = new Color(color.r, color.g, color.b, 0f);
                var yaw = (i == 0 ? -38f : 38f);
                var dir = Quaternion.AngleAxis(yaw, Vector3.up) * (away + Vector3.up * 0.2f);
                arm.SetPosition(0, pos);
                arm.SetPosition(1, pos + dir.normalized * 0.28f);
                extra[i] = arm;
            }

            _sparks.Add(new Spark { Root = go, Line = lr, Arms = extra, Until = Time.time + 0.18f });
            if (_sparks.Count > MaxSparks)
            {
                DestroySpark(_sparks[0]);
                _sparks.RemoveAt(0);
            }
        }

        private void SpawnPopup(Vector3 world, string text, Color color)
        {
            _popups.Add(new Popup
            {
                World = world + Vector3.up * 1.15f,
                Text = text,
                Color = color,
                Born = Time.time,
                Life = 0.7f
            });
            if (_popups.Count > MaxPopups)
                _popups.RemoveAt(0);
        }

        private void LateUpdate()
        {
            var now = Time.time;
            for (var i = _sparks.Count - 1; i >= 0; i--)
            {
                var s = _sparks[i];
                if (s.Root == null || now >= s.Until)
                {
                    DestroySpark(s);
                    _sparks.RemoveAt(i);
                    continue;
                }

                var t = 1f - (s.Until - now) / 0.18f;
                var grow = 1f + t * 1.4f;
                if (s.Line != null && s.Line.positionCount >= 2)
                {
                    var a = s.Line.GetPosition(0);
                    var b = s.Line.GetPosition(1);
                    s.Line.SetPosition(1, a + (b - a).normalized * (0.35f * grow));
                    var c = s.Line.startColor;
                    c.a = 1f - t;
                    s.Line.startColor = c;
                }
            }

            for (var i = _popups.Count - 1; i >= 0; i--)
            {
                if (now - _popups[i].Born >= _popups[i].Life)
                    _popups.RemoveAt(i);
            }
        }

        private void OnGUI()
        {
            if (_popups.Count == 0) return;
            var cam = Camera.main;
            if (cam == null) return;
            EnsureStyle();

            for (var i = 0; i < _popups.Count; i++)
            {
                var p = _popups[i];
                var age = Time.time - p.Born;
                var t = Mathf.Clamp01(age / p.Life);
                var world = p.World + Vector3.up * (0.55f * t);
                var screen = cam.WorldToScreenPoint(world);
                if (screen.z <= 0.1f) continue;

                var alpha = t < 0.7f ? 1f : 1f - (t - 0.7f) / 0.3f;
                var size = 22f + (1f - t) * 8f;
                _popupStyle.fontSize = Mathf.RoundToInt(size);
                var col = p.Color;
                col.a *= alpha;
                GUI.color = col;
                var rect = new Rect(screen.x - 50f, Screen.height - screen.y - 18f, 100f, 28f);
                GUI.Label(rect, p.Text, _popupStyle);
            }

            GUI.color = Color.white;
        }

        private void EnsureStyle()
        {
            if (_popupStyle != null) return;
            _popupStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                fontSize = 22
            };
        }

        private static void DestroySpark(Spark s)
        {
            if (s.Root != null)
                Destroy(s.Root);
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
            for (var i = 0; i < _sparks.Count; i++)
                DestroySpark(_sparks[i]);
            _sparks.Clear();
        }

        private struct Spark
        {
            public GameObject Root;
            public LineRenderer Line;
            public LineRenderer[] Arms;
            public float Until;
        }

        private struct Popup
        {
            public Vector3 World;
            public string Text;
            public Color Color;
            public float Born;
            public float Life;
        }
    }
}
