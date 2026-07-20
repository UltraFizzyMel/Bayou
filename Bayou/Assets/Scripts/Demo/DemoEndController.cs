using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Bayou.Demo
{
    /// <summary>
    /// Fullscreen "demo complete" overlay. Call <see cref="Show"/> when the lantern is found.
    /// </summary>
    public sealed class DemoEndController : MonoBehaviour
    {
        public static DemoEndController Instance { get; private set; }

        [SerializeField] private string title = "Demo Complete";
        [SerializeField] private string body =
            "You found the lantern.\nThanks for playing this Bayou demo.";
        [SerializeField] private bool pauseGame = true;
        [SerializeField] private bool quitOnEscape = true;

        private bool _visible;
        private GUIStyle _titleStyle;
        private GUIStyle _bodyStyle;
        private GUIStyle _hintStyle;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public static void Show()
        {
            var existing = Instance ?? FindFirstObjectByType<DemoEndController>();
            if (existing == null)
            {
                var go = new GameObject("DemoEndController");
                existing = go.AddComponent<DemoEndController>();
            }

            existing.Reveal();
        }

        public void Reveal()
        {
            if (_visible) return;
            _visible = true;
            if (pauseGame)
                Time.timeScale = 0f;
            Debug.Log("[Demo] Complete — lantern found.");
        }

        private void Update()
        {
            if (!_visible || !quitOnEscape) return;
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null && (kb.escapeKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame))
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            }
#endif
        }

        private void OnGUI()
        {
            if (!_visible) return;
            EnsureStyles();

            var dim = new Color(0.02f, 0.04f, 0.05f, 0.82f);
            var old = GUI.color;
            GUI.color = dim;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = old;

            var area = new Rect(Screen.width * 0.2f, Screen.height * 0.28f, Screen.width * 0.6f, Screen.height * 0.4f);
            GUILayout.BeginArea(area);
            GUILayout.Label(title, _titleStyle);
            GUILayout.Space(16f);
            GUILayout.Label(body, _bodyStyle);
            GUILayout.Space(28f);
            GUILayout.Label("Press Enter or Esc to quit", _hintStyle);
            GUILayout.EndArea();
        }

        private void EnsureStyles()
        {
            if (_titleStyle != null) return;
            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 42,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperCenter,
                wordWrap = true,
                normal = { textColor = new Color(0.92f, 0.86f, 0.62f) }
            };
            _bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                alignment = TextAnchor.UpperCenter,
                wordWrap = true,
                normal = { textColor = new Color(0.88f, 0.9f, 0.88f) }
            };
            _hintStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                alignment = TextAnchor.UpperCenter,
                normal = { textColor = new Color(0.65f, 0.7f, 0.68f) }
            };
        }
    }
}
