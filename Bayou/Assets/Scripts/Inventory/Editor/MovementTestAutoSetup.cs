#if UNITY_EDITOR
using System.IO;
using Bayou.Inventory;
using Bayou.Inventory.Shop;
using Bayou.Inventory.UI;
using Bayou.Save;
using Bayou.Testing;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Bayou.Inventory.Editor
{
    /// <summary>
    /// One-shot: drop a marker file, wait for domain reload, then wire MovementTest systems.
    /// Marker path: Temp/BayouSetupMovementTest.flag
    /// </summary>
    [InitializeOnLoad]
    public static class MovementTestAutoSetup
    {
        private const string FlagPath = "Temp/BayouSetupMovementTest.flag";
        private const string ScenePath = "Assets/Scenes/MovementTest.unity";
        private const string LogPath = "Temp/BayouSetupMovementTest.log";

        static MovementTestAutoSetup()
        {
            EditorApplication.delayCall += TryRun;
        }

        [MenuItem("Bayou/Test/Setup MovementTest Systems Now", false, 1)]
        public static void SetupNow()
        {
            ClearFlag();
            RunSetup(interactiveSavePrompt: true);
        }

        /// <summary>Non-interactive entry used by asset-postprocessor / auto flag.</summary>
        public static void SetupNowSilent()
        {
            ClearFlag();
            RunSetup(interactiveSavePrompt: false);
        }

        private static void ClearFlag()
        {
            try
            {
                if (File.Exists(FlagPath))
                    File.Delete(FlagPath);
            }
            catch
            {
                // ignore
            }
        }

        [MenuItem("Bayou/Test/Queue Auto-Setup For MovementTest (next reload)", false, 2)]
        public static void QueueAutoSetup()
        {
            Directory.CreateDirectory("Temp");
            File.WriteAllText(FlagPath, "1");
            WriteLog("QUEUED");
            Debug.Log("[Bayou] Queued MovementTest auto-setup. Triggering script reload…");
            EditorUtility.RequestScriptReload();
        }

        /// <summary>Batchmode entry: Unity.exe -batchmode -executeMethod Bayou.Inventory.Editor.MovementTestAutoSetup.SetupFromBatch</summary>
        public static void SetupFromBatch()
        {
            RunSetup(interactiveSavePrompt: false);
            EditorApplication.Exit(0);
        }

        private static void TryRun()
        {
            if (!File.Exists(FlagPath))
                return;

            try { File.Delete(FlagPath); }
            catch { /* continue */ }

            RunSetup(interactiveSavePrompt: false);
        }

        private static void RunSetup(bool interactiveSavePrompt)
        {
            if (!File.Exists(ScenePath))
            {
                WriteLog("FAILED: MovementTest.unity not found.");
                return;
            }

            try
            {
                if (interactiveSavePrompt)
                {
                    if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    {
                        WriteLog("CANCELLED: user declined saving open scenes.");
                        return;
                    }
                }
                else if (EditorSceneManager.GetActiveScene().isDirty)
                {
                    EditorSceneManager.SaveOpenScenes();
                }

                var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                // Handmade brown UIGrid first — never leave procedural InventoryCanvas behind.
                HandmadeInventorySetupMenu.EnsureHandmadeInventoryInScene(removeProceduralInventory: true);
                PlaytestSetupMenu.SetupPlaytestScene(preservePlayerPosition: true);
                HandmadeInventorySetupMenu.EnsureHandmadeInventoryInScene(removeProceduralInventory: true);
                HandmadeInventorySetupMenu.WireShopToHandmade();
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);

                var hasHandmade = Object.FindFirstObjectByType<InventoryDisplayUI>() != null;
                var hasProcedural = Object.FindFirstObjectByType<InventoryUIController>() != null;
                var hasShop = Object.FindFirstObjectByType<ShopUIController>() != null;
                var hasBonfire = Object.FindFirstObjectByType<BonfireUIController>() != null;
                var hasHarness = Object.FindFirstObjectByType<PlaytestHarness>() != null;

                WriteLog(
                    $"OK handmade={hasHandmade} proceduralLeft={hasProcedural} shop={hasShop} bonfire={hasBonfire} harness={hasHarness}");
                Debug.Log(
                    "[Bayou] MovementTest ready with brown MockUI UIGrid. I=bag, E=interact, ` =HUD, Shift+5=shop.");
            }
            catch (System.Exception ex)
            {
                WriteLog("FAILED: " + ex);
                Debug.LogError("[Bayou] MovementTest setup failed:\n" + ex);
            }
        }

        private static void WriteLog(string message)
        {
            try
            {
                Directory.CreateDirectory("Temp");
                File.WriteAllText(LogPath, message);
            }
            catch
            {
                // Best-effort logging only.
            }
        }
    }
}
#endif

