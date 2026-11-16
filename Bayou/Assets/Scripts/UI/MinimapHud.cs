using System.Collections.Generic;
using Bayou;
using Bayou.Inventory;
using Bayou.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Bayou.UI
{
    /// <summary>
    /// Top-left prototype. A downward camera draws the ground; the arrow is the player.
    /// </summary>
    public sealed class MinimapHud : MonoBehaviour
    {
        public static void Ensure()
        {
            if (Object.FindFirstObjectByType<MinimapHud>() != null) return;
            var go = new GameObject(nameof(MinimapHud));
            go.AddComponent<MinimapHud>();
        }

        private Camera _camera;
        private float _nextMapRender;
        private RectTransform _arrow;
        private RectTransform _map;
        private RenderTexture _texture;
        private readonly List<RectTransform> _markers = new();
        private readonly List<Vector3> _targets = new();

        private void Awake()
        {
            _texture = new RenderTexture(256, 256, 16);
            _texture.name = "MinimapRT";

            var camGo = new GameObject("MinimapCamera");
            camGo.transform.SetParent(transform, false);
            _camera = camGo.AddComponent<Camera>();
            _camera.orthographic = true;
            _camera.orthographicSize = 28f;
            _camera.nearClipPlane = 0.3f;
            _camera.farClipPlane = 80f;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.08f, 0.12f, 0.08f, 1f);
            _camera.cullingMask = ~(1 << 5);
            _camera.targetTexture = _texture;
            _camera.depth = -20f;
            // The map is a second full-scene camera. Leave it off and draw it on a timer.
            _camera.enabled = false;
            camGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            var canvasGo = new GameObject("MinimapCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 18;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var frame = CreateRect("Frame", canvasGo.transform);
            frame.anchorMin = frame.anchorMax = new Vector2(0f, 1f);
            frame.pivot = new Vector2(0f, 1f);
            frame.anchoredPosition = new Vector2(18f, -18f);
            frame.sizeDelta = new Vector2(210f, 210f);
            var frameImage = frame.gameObject.AddComponent<Image>();
            frameImage.color = new Color(0.05f, 0.06f, 0.04f, 0.9f);
            frameImage.raycastTarget = false;

            var map = CreateRect("Map", frame);
            map.anchorMin = map.anchorMax = new Vector2(0.5f, 0.5f);
            map.sizeDelta = new Vector2(190f, 190f);
            _map = map;
            var raw = map.gameObject.AddComponent<RawImage>();
            raw.texture = _texture;
            raw.raycastTarget = false;

            var arrow = CreateRect("Player", map);
            arrow.sizeDelta = new Vector2(16f, 22f);
            _arrow = arrow;
            var arrowImage = arrow.gameObject.AddComponent<Image>();
            arrowImage.color = new Color(0.95f, 0.86f, 0.45f, 1f);
            arrowImage.raycastTarget = false;

            var caption = CreateRect("Caption", frame);
            caption.anchorMin = new Vector2(0f, 1f);
            caption.anchorMax = new Vector2(1f, 1f);
            caption.pivot = new Vector2(0.5f, 1f);
            caption.anchoredPosition = new Vector2(0f, 2f);
            caption.sizeDelta = new Vector2(0f, 22f);
            var label = caption.gameObject.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.text = "N";
            label.fontSize = 16;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = new Color(0.9f, 0.86f, 0.7f, 1f);
            label.raycastTarget = false;
        }

        private void LateUpdate()
        {
            var player = PlayerLocator.Transform;
            if (player == null || _camera == null) return;

            if (_arrow != null)
                _arrow.localEulerAngles = new Vector3(0f, 0f, -player.eulerAngles.y);

            // Second full-scene camera.Render() while timeScale is 0 (catch / bag flow)
            // spiked GPU time and showed as tearing in player builds. Keep the arrow only.
            if (GameplayPause.IsPaused || CaughtFishPresenter.IsBusy)
                return;

            var pos = player.position;
            _camera.transform.position = new Vector3(pos.x, pos.y + 40f, pos.z);
            if (Time.unscaledTime >= _nextMapRender)
            {
                _nextMapRender = Time.unscaledTime + 0.2f;
                _camera.Render();
                CollectTargets();
            }

            DrawQuestMarkers(pos);
        }

        private void DrawQuestMarkers(Vector3 playerPos)
        {
            if (_map == null || _camera == null) return;
            var mapHalf = _map.sizeDelta.x * 0.5f;
            var worldHalf = Mathf.Max(1f, _camera.orthographicSize);
            var scale = mapHalf / worldHalf;
            var limit = mapHalf - 12f;

            for (var i = 0; i < _targets.Count; i++)
            {
                var marker = Marker(i);
                var delta = _targets[i] - playerPos;
                var point = new Vector2(delta.x * scale, delta.z * scale);
                if (point.magnitude > limit)
                    point = point.normalized * limit;
                marker.anchoredPosition = point;
                marker.gameObject.SetActive(true);
            }

            for (var i = _targets.Count; i < _markers.Count; i++)
            {
                if (_markers[i] != null)
                    _markers[i].gameObject.SetActive(false);
            }
        }

        private void CollectTargets()
        {
            _targets.Clear();
            var manager = QuestManager.Resolve();
            var player = PlayerLocator.Transform;
            if (manager == null) return;
            foreach (var quest in manager.AllQuests)
            {
                if (quest == null || !quest.IsActiveForHud) continue;
                if (!QuestObjectiveLocator.TryResolve(quest, manager, player, out var objective))
                    continue;
                var pos = objective.WorldPosition;
                var stacked = false;
                for (var i = 0; i < _targets.Count; i++)
                {
                    var d = _targets[i] - pos;
                    d.y = 0f;
                    if (d.sqrMagnitude < 2.25f)
                    {
                        stacked = true;
                        break;
                    }
                }

                if (!stacked)
                    _targets.Add(pos);
            }
        }

        private RectTransform Marker(int index)
        {
            while (_markers.Count <= index)
            {
                var rt = CreateRect("QuestMarker", _map);
                rt.sizeDelta = new Vector2(14f, 14f);
                rt.localRotation = Quaternion.Euler(0f, 0f, 45f);
                var image = rt.gameObject.AddComponent<Image>();
                image.color = new Color(0.92f, 0.78f, 0.32f, 1f);
                image.raycastTarget = false;
                _markers.Add(rt);
            }

            return _markers[index];
        }

        private void OnDestroy()
        {
            if (_camera != null)
                _camera.targetTexture = null;
            if (_texture != null)
                _texture.Release();
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }
    }
}
