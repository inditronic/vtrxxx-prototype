using UnityEngine;
using UnityEditor;
using UnityEditor.Build.Reporting;
using System.IO;

namespace Ghostclub.Editor
{
    public static class GhostclubAppBuilder
    {
        [MenuItem("Ghostclub/Build Standalone Mac App")]
        public static void BuildMacApp()
        {
            Debug.Log("[Ghostclub] Starting Clean Standalone macOS App Build...");

            // 1. Build Scene
            GhostclubSceneBuilder.BuildScene();

            string appName = "VTRxxx_Prototype.app";
            string desktopPath = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop), appName);
            string projectBuildDir = Path.Combine(Application.dataPath, "..", "Builds", "macOS");
            if (!Directory.Exists(projectBuildDir)) Directory.CreateDirectory(projectBuildDir);
            string projectBuildPath = Path.Combine(projectBuildDir, appName);

            // 2. Clean previous build on Desktop to guarantee fresh timestamp
            if (Directory.Exists(desktopPath))
            {
                try
                {
                    Directory.Delete(desktopPath, true);
                    Debug.Log($"[VTRxxx] Deleted old build at: {desktopPath}");
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[VTRxxx] Cleanup notice: {ex.Message}");
                }
            }

            // 3. Set Player Settings
            PlayerSettings.productName = "VTRxxx Prototype";
            PlayerSettings.companyName = "VTRxxx";

            string[] scenes = new string[]
            {
                "Assets/GhostclubWorld/Scenes/Ghostclub_5_0.unity"
            };

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = desktopPath,
                target = BuildTarget.StandaloneOSX,
                options = BuildOptions.None
            };

            Debug.Log($"[Ghostclub] Compiling and packaging standalone player to: {desktopPath}...");
            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[Ghostclub] BUILD SUCCEEDED! Total size: {summary.totalSize / (1024 * 1024)} MB");
                Debug.Log($"[Ghostclub] Ready to run directly on Mac at: {desktopPath}");

                // Ensure yt-dlp executable permissions inside bundled app
                string bundledYtDlp = Path.Combine(desktopPath, "Contents", "Resources", "Data", "StreamingAssets", "yt-dlp");
                if (File.Exists(bundledYtDlp))
                {
                    try
                    {
                        var proc = System.Diagnostics.Process.Start("chmod", $"+x \"{bundledYtDlp}\"");
                        proc?.WaitForExit(2000);
                    }
                    catch { }
                }

                // Backup copy
                try
                {
                    if (Directory.Exists(projectBuildPath)) Directory.Delete(projectBuildPath, true);
                    FileUtil.CopyFileOrDirectory(desktopPath, projectBuildPath);
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[Ghostclub] Note on backup copy: {ex.Message}");
                }
            }
            else if (summary.result == BuildResult.Failed)
            {
                Debug.LogError($"[Ghostclub] Build failed with {summary.totalErrors} errors!");
            }
        }
    }
}
