using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using XRim.Bots;
using XRim.Bots.Balance;
using XRim.Config;
using XRim.Presentation.Feel;
using XRim.Rules.Settings;
using XRim.Simulation;
using XRim.Simulation.Unity2D;

namespace XRim.Tests.PlayMode.App
{
    /// <summary>
    /// A short bot-vs-bot balance batch from the Test Runner (Sessions 09 and 16): the same engine-free batch the Editor's XRim → Balance →
    /// Run Bot Batch… plays, here 20 matches of the heuristic bot against the random one on real physics with the project's tuning, its report
    /// written to Logs/XRimBotBatch.txt. Explicit, so it runs only when picked. The full batch with the durability sweep is the Editor window's.
    /// </summary>
    public sealed class BotBatchPlayModeTests
    {
        private const int Matches = 20;
        private const string ReportFolderName = "Logs";
        private const string ReportFileName = "XRimBotBatch.txt";
        private const float SceneUnloadTimeoutSeconds = 5f;

        /// <summary>Turns played per frame: the scenes the turns leave behind unload between frames.</summary>
        private const int TurnsPerFrame = 4;

        [UnityTest]
        [Explicit("Plays 20 bot-vs-bot matches on real physics (under a minute); run it by hand from the Test Runner.")]
        [Category("BotBatch")]
        public IEnumerator HeuristicAgainstRandom_Batch_WritesAReport()
        {
            TuningProfile tuning = ProjectTuning.Load();
            if (tuning == null) Assert.Ignore("No tuning profile: run XRim/Setup/Create Default Tuning Assets first.");

            int scenesBefore = SceneManager.sceneCount;
            var issues = new List<string>();
            RulesSettings rules = tuning.BuildRulesSettings(issues);
            SimulationSettings simulation = tuning.BuildSimulationSettings(issues);
            BotSettings bots = tuning.BuildBotSettings(issues);
            BalanceTargetsConfig targetsAsset = tuning.BalanceTargets != null ? tuning.BalanceTargets : ScriptableObject.CreateInstance<BalanceTargetsConfig>();
            BalanceTargets targets = targetsAsset.CreateTargets();
            if (tuning.BalanceTargets == null) Object.Destroy(targetsAsset);
            var spec = new BalanceBatchSpec
            {
                MatchesPerRun = Matches, SweepDurability = false,
                Matchups = new List<BalanceMatchup> { new BalanceMatchup(BotBrainKind.Heuristic, BotBrainKind.Random) },
            };

            FeelConfig feel = tuning.Feel;
            HitStopSettings hitStops = feel != null ? HitStopSettings.From(feel) : null;
            SlowMotionSettings slowMotion = feel != null ? SlowMotionSettings.From(feel) : null;
            var physics = new HeadlessPhysics(rules, simulation, tuning.ArenaSpace);
            var batch = new BalanceBatch(spec, () => tuning.BuildRulesSettings(new List<string>()), simulation, bots, physics.CreateSimulator(),
                feel != null ? turn => FeelDirector.PlannedRealSecondsOf(turn, hitStops, SlowMotionPlanner.Plan(turn, slowMotion)) : null);
            try
            {
                bool more = true;
                while (more)
                {
                    for (int i = 0; i < TurnsPerFrame && more; i++) more = batch.Step();
                    yield return null;
                }
            }
            finally
            {
                batch.Dispose();
                physics.Dispose();
            }

            float until = Time.realtimeSinceStartup + SceneUnloadTimeoutSeconds;
            yield return new WaitUntil(() => SceneManager.sceneCount <= scenesBefore || Time.realtimeSinceStartup > until);

            string report = BalanceReport.Write(batch.Results, spec, targets, rules,
                new[] { $"From the Test Runner, tuning profile '{tuning.name}', {simulation.Segmentation}, {simulation.StepRateHz} Hz." });
            string folder = Path.Combine(Application.dataPath, "..", ReportFolderName);
            Directory.CreateDirectory(folder);
            string file = Path.GetFullPath(Path.Combine(folder, ReportFileName));
            File.WriteAllText(file, report);
            Debug.Log($"[XRim] Bot batch saved to {file}");

            Assert.That(batch.MatchesDone, Is.EqualTo(Matches));
            foreach (BalanceMatchRecord record in batch.Results[0].Records)
            {
                Assert.That(record.Finished, Is.True, $"seed {record.Seed} ended (sudden death always ends, Session 12)");
            }
        }
    }
}
