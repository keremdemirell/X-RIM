using System.IO;
using System.Linq;
using UnityEditor;
using XRim.DebugTools;
using XRim.Editor.PlayModeJobs;
using XRim.Editor.Scenarios;

namespace XRim.Editor
{
    /// <summary>
    /// XRim → Scenarios (Session 16). Scenarios are saved turns (board, both plans, settings, what the turn did): the Playback tab of the
    /// debug overlay saves and replays them; these menu items open their folders.
    /// </summary>
    internal static class ScenarioMenus
    {
        /// <summary>
        /// Resolves the five seed scenarios on real physics and writes the ones that show their moment into the regression tests' folder
        /// (<see cref="SeedFixtureJob"/>; it runs in Play mode by itself). Asks first when they exist: writing them again re-records what each
        /// turn must do from the current code.
        /// </summary>
        [MenuItem(EditorPaths.MenuScenarios + "Write Seed Fixtures")]
        private static void WriteSeedFixtures()
        {
            string folder = ScenarioStore.RegressionFolder();
            bool exist = SeedScenarios.All().Any(recipe => File.Exists(Path.Combine(folder, recipe.FileName + ScenarioStore.FileExtension)));
            if (exist && !EditorUtility.DisplayDialog("Write Seed Fixtures",
                    "The seed fixtures exist already. Writing them again re-records what each turn must do from the current code, so the regression " +
                    "tests stop catching a change already made. Do it only when a change is meant to alter these turns.", "Re-record", "Cancel"))
                return;

            PlayModeJobRunner.Start(PlayModeJobKinds.SeedFixtures, string.Empty);
        }

        /// <summary>Shows the saved scenarios (<c>Logs/Scenarios/</c>, not in git) in the file explorer.</summary>
        [MenuItem(EditorPaths.MenuScenarios + "Open Scenario Folder")]
        private static void OpenScenarioFolder() => Reveal(ScenarioStore.DefaultFolder());

        /// <summary>Shows the regression tests' scenarios (<c>Assets/XRim/Tests/PlayMode/Scenarios/</c>, committed) in the file explorer.</summary>
        [MenuItem(EditorPaths.MenuScenarios + "Open Regression Test Folder")]
        private static void OpenRegressionFolder()
        {
            EditorPaths.EnsureFolder(ScenarioStore.RegressionFolderInProject);
            Reveal(ScenarioStore.RegressionFolder());
        }

        private static void Reveal(string folder)
        {
            Directory.CreateDirectory(folder);
            string[] files = Directory.GetFiles(folder, "*" + ScenarioStore.FileExtension);
            EditorUtility.RevealInFinder(files.Length > 0 ? files[0] : folder);
        }
    }
}
