using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace XRim.Editor
{
    /// <summary>
    /// Menus <c>XRim/Build/Android Development Build</c> and <c>Android Development Build And Run</c> (Session 10): a development APK of
    /// the Match scene, built with <see cref="BuildPipeline"/> into <c>Builds/Android/XRim-dev.apk</c>. A development build defines
    /// <c>DEVELOPMENT_BUILD</c>, so the debug overlay (DebugTools) is in it. The scripting backend is IL2CPP and the architecture ARM64
    /// (<see cref="AndroidPlayerSettingsApplier"/>); the first IL2CPP build compiles everything and is the slow one, later builds are
    /// incremental. Landscape comes from <see cref="ProjectSettingsApplier"/>. "And Run" installs and starts the app through adb on a
    /// connected phone (USB debugging on). If the Editor's build target is not Android, the menu switches it first (the Editor re-imports
    /// its assets and reloads its scripts), then builds by itself.
    /// </summary>
    public static class AndroidBuildMenu
    {
        private const string OutputFolder = "Builds/Android";
        private const string DevelopmentApkName = "XRim-dev.apk";
        private const string AdbRelativePath = "/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb.exe";
        private const string PendingBuildKey = "XRim.AndroidBuild.PendingMode";
        private const int NoPendingBuild = 0;
        private const double BytesPerMegabyte = 1024.0 * 1024.0;

        private enum BuildMode
        {
            Build = 1,
            BuildAndRun = 2,
        }

        [MenuItem(EditorPaths.MenuBuild + "Android Development Build", priority = 1)]
        public static void BuildDevelopment() => Start(BuildMode.Build);

        [MenuItem(EditorPaths.MenuBuild + "Android Development Build And Run", priority = 2)]
        public static void BuildDevelopmentAndRun() => Start(BuildMode.BuildAndRun);

        /// <summary>After the Editor reloaded its scripts for the switch to Android, carry on with the build that asked for it.</summary>
        [InitializeOnLoadMethod]
        private static void ResumeAfterReload()
        {
            if (SessionState.GetInt(PendingBuildKey, NoPendingBuild) != NoPendingBuild) EditorApplication.update += ResumeWhenReady;
        }

        private static void Start(BuildMode mode)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[XRim] Stop Play mode before building.");
                return;
            }

            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
            {
                Debug.LogError("[XRim] Android Build Support is not installed for this Editor. Unity Hub > Installs > the gear on this version > " +
                               "Add modules > Android Build Support (with OpenJDK and Android SDK & NDK Tools).");
                return;
            }

            if (string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(EditorPaths.MatchScenePath)))
            {
                Debug.LogError($"[XRim] {EditorPaths.MatchScenePath} does not exist. Run XRim/Setup/Create Match Scene first.");
                return;
            }

            // The HUD text needs the panel settings asset in the build; the Match scene references it when it was built by Create Match Scene.
            if (Array.IndexOf(AssetDatabase.GetDependencies(EditorPaths.MatchScenePath, true), EditorPaths.HudPanelSettingsPath) < 0)
            {
                Debug.LogError($"[XRim] {EditorPaths.MatchScenePath} does not reference {EditorPaths.HudPanelSettingsPath}, so no HUD text would draw " +
                               "on the phone. Run XRim/Setup/Create Match Scene (it creates the asset and wires it), then build again.");
                return;
            }

            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android)
            {
                Build(mode);
                return;
            }

            // Switching re-imports assets and reloads the scripts, which ends this method: the build carries on from ResumeWhenReady.
            SessionState.SetInt(PendingBuildKey, (int)mode);
            Debug.Log("[XRim] Switching the Editor's build target to Android (assets re-import, which can take several minutes). " +
                      "The build starts by itself when that is done.");
            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(NamedBuildTarget.Android, BuildTarget.Android))
            {
                SessionState.EraseInt(PendingBuildKey);
                Debug.LogError("[XRim] Switching the build target to Android failed (see the Console).");
                return;
            }

            EditorApplication.update += ResumeWhenReady;
        }

        private static void ResumeWhenReady()
        {
            int pending = SessionState.GetInt(PendingBuildKey, NoPendingBuild);
            if (pending == NoPendingBuild)
            {
                EditorApplication.update -= ResumeWhenReady;
                return;
            }

            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;

            EditorApplication.update -= ResumeWhenReady;
            SessionState.EraseInt(PendingBuildKey);
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                Debug.LogError("[XRim] The Editor is not on the Android build target, so the build did not start. Run the menu again.");
                return;
            }

            Build((BuildMode)pending);
        }

        private static void Build(BuildMode mode)
        {
            // Landscape, the Android player settings and Physics 2D in Script mode, as XRim/Setup/Apply Project Settings does.
            ProjectSettingsApplier.Apply();

            Directory.CreateDirectory(OutputFolder);
            string apkPath = OutputFolder + "/" + DevelopmentApkName;
            var options = new BuildPlayerOptions
            {
                scenes = new[] { EditorPaths.MatchScenePath },
                locationPathName = apkPath,
                target = BuildTarget.Android,
                options = BuildOptions.Development | (mode == BuildMode.BuildAndRun ? BuildOptions.AutoRunPlayer : BuildOptions.None),
            };

            bool previousAppBundle = EditorUserBuildSettings.buildAppBundle;
            EditorUserBuildSettings.buildAppBundle = false; // an APK installs directly; a bundle is for the store (Session 22)
            Debug.Log($"[XRim] Building the Android development build (IL2CPP, ARM64) of {EditorPaths.MatchScenePath} into {apkPath}. " +
                      "The first build compiles everything and takes a while.");
            try
            {
                Report(BuildPipeline.BuildPlayer(options), apkPath);
            }
            finally
            {
                EditorUserBuildSettings.buildAppBundle = previousAppBundle;
            }
        }

        private static void Report(BuildReport report, string apkPath)
        {
            BuildSummary summary = report.summary;
            if (summary.result != BuildResult.Succeeded)
            {
                Debug.LogError($"[XRim] Android development build {summary.result}: {summary.totalErrors} error(s), {summary.totalWarnings} warning(s). " +
                               "The errors are in the Console above and in the Editor log.");
                return;
            }

            // summary.totalSize counts the whole output folder (debug symbols included), not the APK that goes on the phone.
            string adb = EditorApplication.applicationContentsPath + AdbRelativePath;
            double apkMegabytes = new FileInfo(apkPath).Length / BytesPerMegabyte;
            Debug.Log($"[XRim] Android development build succeeded: {Path.GetFullPath(apkPath)} (APK {apkMegabytes:F1} MB, " +
                      $"{summary.totalTime.TotalMinutes:F1} min, {summary.totalWarnings} warning(s)). The build is fine even if an install error " +
                      "appears above: that comes from the phone, not the build. To install it by hand on a phone with USB debugging on: " +
                      $"\"{adb}\" install -r \"{Path.GetFullPath(apkPath)}\"");
        }
    }
}
