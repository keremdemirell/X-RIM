using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using XRim.Config;
using XRim.Simulation.Recording;
using XRim.Simulation.Scenarios;
using XRim.Simulation.Unity2D;

namespace XRim.Tests.PlayMode.Simulation
{
    /// <summary>
    /// The scenario regression tests (Session 16): every saved scenario in the regression folder is resolved again on real physics, on a dummy
    /// built from its own settings, and must repeat the outcome it recorded (the same events in the same order, times and amounts within
    /// <see cref="ScenarioTolerance.Default"/>, the same ending). The same inputs resolved twice must give exactly the same turn, pose for pose.
    /// The five seed moments come from XRim → Scenarios → Write Seed Fixtures; the Playback tab's "Add to regression tests" adds more.
    /// </summary>
    public sealed class ScenarioRegressionPlayModeTests
    {
        /// <summary>The regression folder under Assets/ (the debug tools' <c>ScenarioStore.RegressionFolderInProject</c>; tests cannot see them).</summary>
        private const string FolderUnderAssets = "XRim/Tests/PlayMode/Scenarios";

        private const string FileExtension = ".json";
        private const int MinimumFixtures = 5;
        private const string WriteThemHint = "Run XRim → Scenarios → Write Seed Fixtures first (it writes them into Assets/" + FolderUnderAssets + ").";
        private const float SceneUnloadTimeoutSeconds = 5f;

        private static readonly string[] SeedMoments =
        {
            "seed-reference-duck-under-mace", "seed-mace-crushes-rapier", "seed-shield-full-block", "seed-sever-arm", "seed-wall-zap",
        };

        private SimulationMode2D _previousMode;
        private int _scenesBefore;

        [SetUp]
        public void SetUp()
        {
            _previousMode = Physics2D.simulationMode;
            _scenesBefore = SceneManager.sceneCount;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            float until = Time.realtimeSinceStartup + SceneUnloadTimeoutSeconds;
            yield return new WaitUntil(() => SceneManager.sceneCount <= _scenesBefore || Time.realtimeSinceStartup > until);
            Physics2D.simulationMode = _previousMode;
        }

        private static string Folder => Path.Combine(Application.dataPath, FolderUnderAssets);

        private static string[] FixtureFiles() =>
            Directory.Exists(Folder) ? Directory.GetFiles(Folder, "*" + FileExtension).OrderBy(path => path, System.StringComparer.Ordinal).ToArray() : new string[0];

        private static Scenario Load(string path) => ScenarioJson.Read(File.ReadAllText(path));

        private static TurnResult Resolve(Scenario scenario) =>
            HeadlessPhysics.SimulateOnce(scenario.Input, new ArenaSpace(scenario.WorldUnitsPerArenaUnit));

        [Test]
        public void TheRegressionFolder_HoldsTheFiveSeedMoments()
        {
            string[] files = FixtureFiles();
            Assert.That(files.Length, Is.GreaterThanOrEqualTo(MinimumFixtures), $"{files.Length} scenarios in the folder. {WriteThemHint}");
            foreach (string moment in SeedMoments)
            {
                Assert.That(File.Exists(Path.Combine(Folder, moment + FileExtension)), Is.True, $"{moment} is missing. {WriteThemHint}");
            }
        }

        [UnityTest]
        public IEnumerator EverySavedScenario_RepeatsItsRecordedOutcome()
        {
            string[] files = FixtureFiles();
            if (files.Length == 0) Assert.Ignore("No scenarios yet. " + WriteThemHint);

            var failures = new List<string>();
            foreach (string file in files)
            {
                string name = Path.GetFileNameWithoutExtension(file);
                Scenario scenario = Load(file);
                if (scenario.Expected == null)
                {
                    failures.Add($"{name}: it has no recorded outcome to compare with");
                    continue;
                }

                List<string> differences = ScenarioComparison.Differences(scenario.Expected, ScenarioOutcome.Of(Resolve(scenario)), ScenarioTolerance.Default);
                Debug.Log($"[XRim scenario] {name}: " + (differences.Count == 0 ? "repeats its recording" : string.Join("; ", differences)));
                if (differences.Count > 0) failures.Add($"{name} ({scenario.Note}): {string.Join("; ", differences)}");
                yield return null; // the turn's physics scene unloads
            }

            Assert.That(failures, Is.Empty, string.Join("\n", failures));
        }

        [UnityTest]
        public IEnumerator EverySavedScenario_ResolvedTwice_GivesExactlyTheSameTurn()
        {
            string[] files = FixtureFiles();
            if (files.Length == 0) Assert.Ignore("No scenarios yet. " + WriteThemHint);

            var failures = new List<string>();
            foreach (string file in files)
            {
                string name = Path.GetFileNameWithoutExtension(file);
                Scenario scenario = Load(file);
                TurnResult first = Resolve(scenario);
                yield return null;
                TurnResult second = Resolve(scenario);
                yield return null;

                string difference = FirstDifference(first, second);
                if (difference != null) failures.Add($"{name}: {difference}");
            }

            Assert.That(failures, Is.Empty, string.Join("\n", failures));
        }

        /// <summary>Null when two turns are the same in every event and every recorded pose value; otherwise the first place they part.</summary>
        private static string FirstDifference(TurnResult first, TurnResult second)
        {
            List<string> outcome = ScenarioComparison.Differences(ScenarioOutcome.Of(first), ScenarioOutcome.Of(second), ScenarioTolerance.Exact);
            if (outcome.Count > 0) return string.Join("; ", outcome);
            if (first.Timeline.Frames.Count != second.Timeline.Frames.Count)
                return $"{first.Timeline.Frames.Count} recorded frames the first time, {second.Timeline.Frames.Count} the second";
            for (int i = 0; i < first.Timeline.Frames.Count; i++)
            {
                if (JsonUtility.ToJson(first.Timeline.Frames[i].Pose) != JsonUtility.ToJson(second.Timeline.Frames[i].Pose))
                    return $"the poses part at frame {i} ({first.Timeline.Frames[i].Time})";
            }

            return JsonUtility.ToJson(first.FinalBoard.Pose) == JsonUtility.ToJson(second.FinalBoard.Pose) ? null : "the final boards differ";
        }
    }
}
