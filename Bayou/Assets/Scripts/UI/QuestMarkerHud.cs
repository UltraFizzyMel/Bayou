using Bayou.Inventory.Shop;
using Bayou.Save;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Bayou.UI
{
    /// <summary>
    /// Screen-space quest guide: pin when objective is on-screen, edge arrow when off-screen.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class QuestMarkerHud : MonoBehaviour
    {
        [SerializeField] private Canvas canvas;
        [SerializeField] private RectTransform markerRoot;
        [SerializeField] private RectTransform arrow;
        [SerializeField] private Image pinImage;
        [SerializeField] private TextMeshProUGUI distanceLabel;
        [SerializeField] private TextMeshProUGUI nameLabel;
        [SerializeField] private bool buildUiIfMissing = true;
        [SerializeField] private float edgePadding = 48f;
        [SerializeField] private float hideWhenCloserThan = 2.2f;
        [SerializeField] private Color markerColor = new(0.92f, 0.78f, 0.32f, 0.95f);

        private Camera _cam;
        private Transform _player;
        private bool _hasTarget;
        private Vector3 _targetPos;
        private string _targetLabel;
        private bool _isTurnIn;

        public static QuestMarkerHud Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
            if (buildUiIfMissing && (canvas == null || markerRoot == null))
                BuildUi();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void LateUpdate()
        {
            if (ShouldHide())
            {
                SetVisible(false);
                return;
            }

            EnsureRefs();
            if (!TryResolveTarget())
            {
                SetVisible(false);
                return;
            }

            UpdateMarker();
        }

        private bool ShouldHide()
        {
            if (AudioSettings.IsOpen) return true;
            if (ShopUIController.ActiveShop != null && ShopUIController.ActiveShop.IsOpen) return true;
            if (BonfireUIController.Active != null && BonfireUIController.Active.IsOpen) return true;
            var dialogue = DialogueManager.GetInstance();
            if (dialogue != null && dialogue.dialogueIsPlaying) return true;
            return false;
        }

        private void EnsureRefs()
        {
            if (_cam == null)
                _cam = Camera.main;
            if (_player == null)
                _player = Bayou.Player.PlayerLocator.Transform;
        }

        private bool TryResolveTarget()
        {
            _hasTarget = false;
            var manager = QuestManager.Resolve();
            if (manager == null || !manager.TryGetPrimaryActiveQuest(out var quest))
                return false;

            if (!QuestObjectiveLocator.TryResolve(quest, manager, _player, out var objective))
                return false;

            _targetPos = objective.WorldPosition;
            _targetLabel = objective.Label;
            _isTurnIn = objective.IsTurnIn;
            _hasTarget = true;
            return true;
        }

        private void UpdateMarker()
        {
            if (!_hasTarget || _cam == null || markerRoot == null)
            {
                SetVisible(false);
                return;
            }

            var dist = 0f;
            if (_player != null)
            {
                var flat = _targetPos - _player.position;
                flat.y = 0f;
                dist = flat.magnitude;
            }

            if (dist > 0f && dist < hideWhenCloserThan)
            {
                SetVisible(false);
                return;
            }

            SetVisible(true);

            var canvasRt = canvas != null ? canvas.transform as RectTransform : markerRoot.parent as RectTransform;
            if (canvasRt == null)
            {
                SetVisible(false);
                return;
            }

            var screenPoint = _cam.WorldToScreenPoint(_targetPos);
            var behind = screenPoint.z < 0f;
            if (behind)
            {
                screenPoint.x = Screen.width - screenPoint.x;
                screenPoint.y = Screen.height - screenPoint.y;
            }

            var onScreen = !behind &&
                           screenPoint.x > edgePadding && screenPoint.x < Screen.width - edgePadding &&
                           screenPoint.y > edgePadding && screenPoint.y < Screen.height - edgePadding;

            Vector2 localPoint;
            if (onScreen)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRt, screenPoint, null, out localPoint);
                if (arrow != null) arrow.gameObject.SetActive(false);
                if (pinImage != null) pinImage.enabled = true;
            }
            else
            {
                // Direction from screen center toward (possibly flipped) target.
                var center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                var dir = ((Vector2)screenPoint - center);
                if (dir.sqrMagnitude < 0.0001f)
                    dir = Vector2.up;
                dir.Normalize();

                var half = new Vector2(Screen.width * 0.5f - edgePadding, Screen.height * 0.5f - edgePadding);
                var scaleX = Mathf.Abs(dir.x) > 0.001f ? half.x / Mathf.Abs(dir.x) : float.MaxValue;
                var scaleY = Mathf.Abs(dir.y) > 0.001f ? half.y / Mathf.Abs(dir.y) : float.MaxValue;
                var edgeScreen = center + dir * Mathf.Min(scaleX, scaleY);

                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRt, edgeScreen, null, out localPoint);

                if (arrow != null)
                {
                    arrow.gameObject.SetActive(true);
                    var angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
                    arrow.localRotation = Quaternion.Euler(0f, 0f, angle);
                }

                if (pinImage != null) pinImage.enabled = false;
            }

            markerRoot.anchoredPosition = localPoint;

            if (distanceLabel != null)
                distanceLabel.text = dist >= 10f ? $"{dist:0}m" : $"{dist:0.0}m";

            if (nameLabel != null)
            {
                var prefix = _isTurnIn ? "Turn in" : "Go";
                nameLabel.text = string.IsNullOrWhiteSpace(_targetLabel)
                    ? prefix
                    : $"{prefix}: {_targetLabel}";
            }

            // Soft pulse for readability.
            var pulse = 0.92f + 0.08f * Mathf.Sin(Time.unscaledTime * 3.2f);
            markerRoot.localScale = Vector3.one * pulse;
        }

        private void SetVisible(bool visible)
        {
            if (markerRoot != null && markerRoot.gameObject.activeSelf != visible)
                markerRoot.gameObject.SetActive(visible);
        }

        private void BuildUi()
        {
            var canvasGo = new GameObject("QuestMarkerCanvas");
            canvasGo.transform.SetParent(transform, false);
            canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 12;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>().enabled = false;

            var rootGo = new GameObject("Marker", typeof(RectTransform));
            rootGo.transform.SetParent(canvasGo.transform, false);
            markerRoot = rootGo.GetComponent<RectTransform>();
            markerRoot.sizeDelta = new Vector2(160f, 72f);
            markerRoot.anchorMin = markerRoot.anchorMax = new Vector2(0.5f, 0.5f);
            markerRoot.pivot = new Vector2(0.5f, 0.5f);

            // Pin diamond
            var pinGo = new GameObject("Pin", typeof(RectTransform));
            pinGo.transform.SetParent(markerRoot, false);
            var pinRt = pinGo.GetComponent<RectTransform>();
            pinRt.anchoredPosition = new Vector2(0f, 18f);
            pinRt.sizeDelta = new Vector2(22f, 22f);
            pinImage = pinGo.AddComponent<Image>();
            pinImage.color = markerColor;
            pinImage.raycastTarget = false;
            pinRt.localRotation = Quaternion.Euler(0f, 0f, 45f);

            // Edge arrow (triangle via rotated image)
            var arrowGo = new GameObject("Arrow", typeof(RectTransform));
            arrowGo.transform.SetParent(markerRoot, false);
            arrow = arrowGo.GetComponent<RectTransform>();
            arrow.anchoredPosition = new Vector2(0f, 22f);
            arrow.sizeDelta = new Vector2(28f, 28f);
            var arrowImg = arrowGo.AddComponent<Image>();
            arrowImg.color = markerColor;
            arrowImg.raycastTarget = false;
            // Approximate chevron with a diamond rotated as arrow head.
            arrow.localRotation = Quaternion.identity;
            arrowGo.SetActive(false);

            nameLabel = CreateTmp("Label", markerRoot, "Objective", 16f, FontStyles.Bold,
                new Vector2(0f, -10f), new Vector2(200f, 24f));
            distanceLabel = CreateTmp("Distance", markerRoot, "0m", 14f, FontStyles.Normal,
                new Vector2(0f, -30f), new Vector2(80f, 20f));

            markerRoot.gameObject.SetActive(false);
        }

        private static TextMeshProUGUI CreateTmp(
            string name,
            Transform parent,
            string text,
            float size,
            FontStyles style,
            Vector2 anchoredPos,
            Vector2 sizeDelta)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(0.95f, 0.93f, 0.85f, 0.95f);
            tmp.raycastTarget = false;
            if (TMP_Settings.defaultFontAsset != null)
                tmp.font = TMP_Settings.defaultFontAsset;
            return tmp;
        }

        public static QuestMarkerHud EnsureInScene()
        {
            if (Instance != null) return Instance;
            var existing = Object.FindFirstObjectByType<QuestMarkerHud>(FindObjectsInactive.Include);
            if (existing != null) return existing;

            var go = new GameObject("QuestMarkerHud");
            return go.AddComponent<QuestMarkerHud>();
        }
    }
}
