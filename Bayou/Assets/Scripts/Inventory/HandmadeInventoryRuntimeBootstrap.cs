using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Bayou.Inventory
{
    /// <summary>
    /// Player builds: TerrainTest keeps MockUI/UIGrid disabled (scale 0) for editor layout.
    /// Editor play used HandmadeInventoryPlayModeBootstrap to spawn a second bag — builds did not.
    /// </summary>
    public static class HandmadeInventoryRuntimeBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Ensure();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Ensure();

        public static void Ensure()
        {
            if (string.Equals(SceneManager.GetActiveScene().name, "MainMenu", System.StringComparison.OrdinalIgnoreCase))
                return;

            var displays = Object.FindObjectsByType<InventoryDisplayUI>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            InventoryDisplayUI active = null;
            for (var i = 0; i < displays.Length; i++)
            {
                var display = displays[i];
                if (display == null) continue;

                var canvasRoot = display.GetComponentInParent<Canvas>();
                if (canvasRoot == null) continue;

                var root = canvasRoot.gameObject;
                if (root.name == "UIGrid" || display.GetComponentInParent<InventoryDisplayUI>() == display)
                {
                    WakeCanvasRoot(root.transform as RectTransform);
                    if (!root.activeSelf)
                        root.SetActive(true);
                    active = display;
                    break;
                }
            }

            if (active == null)
                WakeOrphanUIGrid();

            if (active != null && InventoryDisplayUI.Active == null)
                Debug.Log("[Inventory] Handmade UIGrid enabled for player build.");
        }

        private static void WakeOrphanUIGrid()
        {
            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < canvases.Length; i++)
            {
                var canvas = canvases[i];
                if (canvas == null || canvas.name != "UIGrid") continue;
                WakeCanvasRoot(canvas.transform as RectTransform);
                if (!canvas.gameObject.activeSelf)
                    canvas.gameObject.SetActive(true);
                return;
            }
        }

        private static void WakeCanvasRoot(RectTransform canvasRt)
        {
            if (canvasRt == null) return;

            if (canvasRt.localScale.sqrMagnitude < 0.0001f)
                canvasRt.localScale = Vector3.one;

            canvasRt.anchorMin = Vector2.zero;
            canvasRt.anchorMax = Vector2.one;
            canvasRt.offsetMin = Vector2.zero;
            canvasRt.offsetMax = Vector2.zero;
            canvasRt.pivot = new Vector2(0.5f, 0.5f);
            canvasRt.localRotation = Quaternion.identity;

            var scaler = canvasRt.GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;
            }
        }
    }
}