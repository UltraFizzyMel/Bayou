#if UNITY_EDITOR
using System.IO;
using UnityEditor;

namespace Bayou.Inventory.Editor
{
    /// <summary>
    /// Fires MovementTest setup when Unity processes assets and a flag file is present.
    /// </summary>
    internal sealed class BayouSetupTrigger : AssetPostprocessor
    {
        private const string FlagPath = "Temp/BayouSetupMovementTest.flag";

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (!File.Exists(FlagPath))
                return;

            EditorApplication.delayCall += () =>
            {
                if (!File.Exists(FlagPath))
                    return;
                MovementTestAutoSetup.SetupNowSilent();
            };
        }
    }
}
#endif
