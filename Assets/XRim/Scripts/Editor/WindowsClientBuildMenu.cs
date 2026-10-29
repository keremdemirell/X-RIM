using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace XRim.Editor
{
    /// <summary>
    /// Menu <c>XRim/Build/Windows Client (Development)</c> (Session 19): the game (the Match scene) as a Windows development build into
    /// <c>Builds/Windows/XRim.exe</c> (git-ignored), so two players can play online on one PC: the Editor as one, this build as the other,
    /// with the server running beside them. The mouse plays as a finger, as in the Editor. If the Editor's build target is not Windows, the
    /// menu switches it first and then builds by itself, like the Android menu; the Android menu switches back when next used.
    /// </summary>
    public static class WindowsClientBuildMenu
    {
        private const string OutputFolder = "Builds/Windows";
        private const string ExecutableName = "XRim.exe";
        private const string PendingBuildKey = "XRim.WindowsClientBuild.Pending";

        /// <summary>A window this size beside the Editor (technical: launch options, not a project setting).</summary>
        private const string WindowedLaunchOptions = "-screen-fullscreen 0 -screen-width 1280 -screen-height 720";

        [MenuItem(EditorPaths.MenuBuild + "Windows Client (Development)", priority = 30)]
        public static void Build() => Start();

        [InitializeOnLoadMethod]
        private static void ResumeAfterReload()
        {
            if (SessionState.GetBool(PendingBuildKey, false)) EditorApplication.update += ResumeWhenReady;
        }

        private static void Start()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[XRim] Stop Play mode before building.");
                return;
            }

            if (string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(EditorPaths.MatchScenePath)))
            {
                Debug.LogError($"[XRim] {EditorPaths.MatchScenePath} does not exist. Run XRim/Setup/Create Match Scene first.");
                return;
            }

            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.StandaloneWindows64 &&
                EditorUserBuildSettings.standaloneBuildSubtarget == StandaloneBuildSubtarget.Player)
            {
                BuildNow();
                return;
            }

            SessionState.SetBool(PendingBuildKey, true);
            Debug.Log("[XRim] Switching the Editor's build target to Windows (assets re-import, which can take several minutes). " +
                      "The build starts by itself when that is done.");
            EditorUserBuildSettings.standaloneBuildSubtarget = StandaloneBuildSubtarget.Player;
            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(NamedBuildTarget.Standalone, BuildTarget.StandaloneWindows64))
            {
                SessionState.EraseBool(PendingBuildKey);
                Debug.LogError("[XRim] Switching the build target to Windows failed (see the Console).");
                return;
            }

            EditorApplication.update += ResumeWhenReady;
        }

        private static void ResumeWhenReady()
        {
            if (!SessionState.GetBool(PendingBuildKey, false))
            {
                EditorApplication.update -= ResumeWhenReady;
                return;
            }

            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;

            EditorApplication.update -= ResumeWhenReady;
            SessionState.EraseBool(PendingBuildKey);
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64)
            {
                Debug.LogError("[XRim] The Editor is not on the Windows build target, so the build did not start. Run the menu again.");
                return;
            }

            BuildNow();
        }

        private static void BuildNow()
        {
            ProjectSettingsApplier.Apply();
            Directory.CreateDirectory(OutputFolder);
            string path = OutputFolder + "/" + ExecutableName;
            var options = new BuildPlayerOptions
            {
                scenes = new[] { EditorPaths.MatchScenePath },
                locationPathName = path,
                target = BuildTarget.StandaloneWindows64,
                subtarget = (int)StandaloneBuildSubtarget.Player,
                options = BuildOptions.Development,
            };

            Debug.Log($"[XRim] Building the Windows development client of {EditorPaths.MatchScenePath} into {path}.");
            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            if (summary.result != BuildResult.Succeeded)
            {
                Debug.LogError($"[XRim] Windows client build {summary.result}: {summary.totalErrors} error(s) (see the Console above).");
                return;
            }

            string full = Path.GetFullPath(path);
            Debug.Log($"[XRim] Windows client build succeeded ({summary.totalTime.TotalMinutes:F1} min). Start it in a window beside the Editor: " +
                      $"\"{full}\" {WindowedLaunchOptions}{Environment.NewLine}On this PC it shares the Editor's saved profile (the same hand and collection).");
        }
    }
}
