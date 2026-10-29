using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace XRim.Editor
{
    /// <summary>
    /// Menus <c>XRim/Build/Windows Dedicated Server</c> and <c>Linux Dedicated Server</c> (Session 19): the online server as a headless
    /// player of the Server scene alone (<see cref="ServerSceneCreator"/>), into <c>Builds/Server/Windows/</c> or <c>Builds/Server/Linux/</c>
    /// (git-ignored). A Dedicated Server player has no graphics and defines <c>UNITY_SERVER</c>, which is what brings <c>XRim.Server</c> into
    /// it. Each needs its module in Unity Hub (Windows or Linux Dedicated Server Build Support); Linux is for a rented server (Session 20),
    /// Windows for the local test on this PC.
    /// <para>
    /// If the Editor's build target is not that server, the menu switches it first (assets re-import, scripts reload) and then builds by
    /// itself, like the Android menu. The phone build (XRim/Build/Android Development Build) switches back when it is next used.
    /// </para>
    /// </summary>
    public static class ServerBuildMenu
    {
        private const string OutputFolder = "Builds/Server";
        private const string ExecutableName = "XRimServer";
        private const string PendingBuildKey = "XRim.ServerBuild.PendingPlatform";
        private const int NoPendingBuild = 0;

        private enum Platform
        {
            Windows = 1,
            Linux = 2,
        }

        [MenuItem(EditorPaths.MenuBuild + "Windows Dedicated Server", priority = 20)]
        public static void BuildWindows() => Start(Platform.Windows);

        [MenuItem(EditorPaths.MenuBuild + "Linux Dedicated Server", priority = 21)]
        public static void BuildLinux() => Start(Platform.Linux);

        [InitializeOnLoadMethod]
        private static void ResumeAfterReload()
        {
            if (SessionState.GetInt(PendingBuildKey, NoPendingBuild) != NoPendingBuild) EditorApplication.update += ResumeWhenReady;
        }

        private static BuildTarget TargetOf(Platform platform) =>
            platform == Platform.Windows ? BuildTarget.StandaloneWindows64 : BuildTarget.StandaloneLinux64;

        private static void Start(Platform platform)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[XRim] Stop Play mode before building.");
                return;
            }

            BuildTarget target = TargetOf(platform);
            if (!IsServerModuleInstalled(platform))
            {
                Debug.LogError($"[XRim] {platform} Dedicated Server Build Support is not installed for this Editor. Unity Hub > Installs > the gear on " +
                               $"this version > Add modules > {platform} Dedicated Server Build Support. (If the Hub does not list this Editor, " +
                               "use Installs > Locate first and pick S:/Unity_Editor/Editor/Unity.exe.)");
                return;
            }

            if (string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(EditorPaths.ServerScenePath)) && !ServerSceneCreator.Build()) return;

            if (EditorUserBuildSettings.activeBuildTarget == target && EditorUserBuildSettings.standaloneBuildSubtarget == StandaloneBuildSubtarget.Server)
            {
                Build(platform);
                return;
            }

            SessionState.SetInt(PendingBuildKey, (int)platform);
            Debug.Log($"[XRim] Switching the Editor's build target to the {platform} Dedicated Server (assets re-import, which can take several minutes). " +
                      "The build starts by itself when that is done.");
            EditorUserBuildSettings.standaloneBuildSubtarget = StandaloneBuildSubtarget.Server;
            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(NamedBuildTarget.Server, target))
            {
                SessionState.EraseInt(PendingBuildKey);
                Debug.LogError($"[XRim] Switching the build target to the {platform} Dedicated Server failed (see the Console).");
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
            var platform = (Platform)pending;
            if (EditorUserBuildSettings.activeBuildTarget != TargetOf(platform))
            {
                Debug.LogError($"[XRim] The Editor is not on the {platform} Dedicated Server target, so the build did not start. Run the menu again.");
                return;
            }

            Build(platform);
        }

        private static void Build(Platform platform)
        {
            string folder = $"{OutputFolder}/{platform}";
            string path = $"{folder}/{ExecutableName}{(platform == Platform.Windows ? ".exe" : ".x86_64")}";
            Directory.CreateDirectory(folder);
            var options = new BuildPlayerOptions
            {
                scenes = new[] { EditorPaths.ServerScenePath },
                locationPathName = path,
                target = TargetOf(platform),
                subtarget = (int)StandaloneBuildSubtarget.Server,
                options = BuildOptions.None,
            };

            Debug.Log($"[XRim] Building the {platform} Dedicated Server of {EditorPaths.ServerScenePath} into {path}.");
            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            if (summary.result != BuildResult.Succeeded)
            {
                Debug.LogError($"[XRim] {platform} Dedicated Server build {summary.result}: {summary.totalErrors} error(s) (see the Console above).");
                return;
            }

            Debug.Log($"[XRim] {platform} Dedicated Server build succeeded: {Path.GetFullPath(path)} ({summary.totalTime.TotalMinutes:F1} min). " +
                      (platform == Platform.Windows
                          ? $"Run it from a console window: \"{Path.GetFullPath(path)}\" -port 7777 (Windows asks once whether it may use the network: allow private networks)."
                          : "Copy the whole folder to the Linux machine and run ./XRimServer.x86_64 -port 7777."));
        }

        /// <summary>The Dedicated Server player of this platform is installed (its variation folder exists).</summary>
        private static bool IsServerModuleInstalled(Platform platform)
        {
            string engines = EditorApplication.applicationContentsPath + "/PlaybackEngines";
            string support = platform == Platform.Windows ? "WindowsStandaloneSupport" : "LinuxStandaloneSupport";
            string serverPrefix = platform == Platform.Windows ? "win64_server" : "linux64_server";
            string variations = $"{engines}/{support}/Variations";
            if (!Directory.Exists(variations)) return false;
            foreach (string folder in Directory.GetDirectories(variations))
            {
                if (Path.GetFileName(folder).StartsWith(serverPrefix, System.StringComparison.OrdinalIgnoreCase)) return true;
            }

            return false;
        }
    }
}
