using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using XRim.App;
using XRim.Config;
using XRim.Core;
using XRim.Networking;
using XRim.Rules;
using XRim.Rules.Events;
using XRim.Rules.Match;
using XRim.Rules.Settings;
using XRim.Simulation;
using XRim.Simulation.Execution;
using XRim.Simulation.Recording;
using XRim.Simulation.Settings;
using XRim.Simulation.Unity2D;
using Object = UnityEngine.Object;

namespace XRim.Tests.PlayMode.App
{
    /// <summary>
    /// Measures how long matches between two random debug bots run on the real physics and rules, with the project's tuning
    /// (GDD §1: target match length is TBD, so this measures, it decides nothing). Headless: no playback, no screen. Explicit, so it
    /// runs only when picked in the Test Runner; it writes Logs/XRimBotBatch.txt. The random bot is not a human, so its numbers are a
    /// baseline for the turn loop, not a prediction of real matches.
    /// </summary>
    public sealed class BotBatchPlayModeTests
    {
        private const int Matches = 20;
        private const uint FirstSeed = 1u;

        /// <summary>A safety net only: sudden death repeats turns until somebody hits.</summary>
        private const int TurnLimit = 150;

        private const string ReportFolderName = "Logs";
        private const string ReportFileName = "XRimBotBatch.txt";
        private const float SceneUnloadTimeoutSeconds = 5f;
        private const int SecondsPerMinute = 60;

        [UnityTest]
        [Explicit("Runs 20 bot-vs-bot matches on real physics (about a minute); run it by hand from the Test Runner.")]
        [Category("BotBatch")]
        public IEnumerator BotVsBot_Batch_MeasuresMatchLength()
        {
            TuningProfile tuning = ProjectTuning.Load();
            if (tuning == null) Assert.Ignore("No tuning profile: run XRim/Setup/Create Default Tuning Assets first.");

            SimulationMode2D previousMode = Physics2D.simulationMode;
            Physics2D.simulationMode = SimulationMode2D.Script;
            int scenesBefore = SceneManager.sceneCount;
            var issues = new List<string>();
            RulesSettings rules = tuning.BuildRulesSettings(issues);
            SimulationSettings simulation = tuning.BuildSimulationSettings(issues);
            ArenaSpace space = tuning.ArenaSpace;
            Ragdoll six = Template(RagdollSegmentation.SixBodies, rules, simulation, space);
            Ragdoll ten = Template(RagdollSegmentation.TenBodies, rules, simulation, space);
            var world = new Unity2DPhysicsWorld(space, new RagdollPrefabSet(six, ten));
            var results = new List<MatchStats>();
            try
            {
                for (uint seed = FirstSeed; seed < FirstSeed + Matches; seed++)
                {
                    var stats = new MatchStats(seed);
                    IEnumerator match = PlayMatch(world, rules, simulation, stats);
                    while (match.MoveNext()) yield return match.Current;
                    results.Add(stats);
                }
            }
            finally
            {
                world.Dispose();
                Object.Destroy(six.gameObject);
                Object.Destroy(ten.gameObject);
                Physics2D.simulationMode = previousMode;
            }

            float until = Time.realtimeSinceStartup + SceneUnloadTimeoutSeconds;
            yield return new WaitUntil(() => SceneManager.sceneCount <= scenesBefore || Time.realtimeSinceStartup > until);

            float hitStopSeconds = tuning.Feel != null ? tuning.Feel.HitStopSeconds : 0f;
            string report = Report(results, rules, simulation, hitStopSeconds, tuning.name);
            string folder = Path.Combine(Application.dataPath, "..", ReportFolderName);
            Directory.CreateDirectory(folder);
            string file = Path.GetFullPath(Path.Combine(folder, ReportFileName));
            File.WriteAllText(file, report);
            Debug.Log($"[XRim] Bot batch saved to {file}\n{report}");

            Assert.That(results.All(r => r.Finished), Is.True, "every match ended (or stopped on the sudden-death tie placeholder)");
        }

        /// <summary>One match, one turn per frame, exactly as the Match scene's bot vs bot plays it minus the screen.</summary>
        private static IEnumerator PlayMatch(Unity2DPhysicsWorld world, RulesSettings rules, SimulationSettings simulation, MatchStats stats)
        {
            var policies = new RulePolicies();
            var simulator = new RecordingTurnSimulator(new TurnSimulator(world, policies));
            MatchSetup setup = CreateSetup(rules, stats.Seed);
            var authority = new LocalTurnAuthority(setup, rules, simulation, policies, simulator, new ManualClock());
            authority.TurnResolved += turn =>
            {
                stats.Add(turn, simulator.LastSimulationMilliseconds);
                if (SuddenDeathTiePlaceholder.StopsTheMatch(turn)) stats.StoppedOnTie = true;
            };
            authority.MatchEnded += outcome => stats.Outcome = outcome;

            IReadOnlyList<IPlanSource> bots = PlanSourceFactory.Create(MatchMode.BotVsBot, setup, rules, NoHuman, new XorShiftRandom(stats.Seed));
            using (new PlanSourceBinder(authority, bots))
            {
                authority.Start();
                while (!stats.Finished && stats.Turns < TurnLimit)
                {
                    authority.Tick(); // one turn per call: the bots are Ready at once and there is no playback to wait for
                    yield return null;
                }
            }
        }

        private static IPlanSource NoHuman(Side side) => throw new InvalidOperationException("Bot vs bot asks for no human.");

        /// <summary>Both right-handed with the default loadout (D8), as the Match scene sets them up.</summary>
        private static MatchSetup CreateSetup(RulesSettings rules, uint seed)
        {
            var loadout = rules.Loadout.DefaultLoadoutWeaponIds.Select(id => new WeaponId(id)).ToList();
            return new MatchSetup(PerSide<FighterSetup>.Create(_ => new FighterSetup(Handedness.Right, loadout)), seed);
        }

        private static Ragdoll Template(RagdollSegmentation segmentation, RulesSettings rules, SimulationSettings simulation, ArenaSpace space)
        {
            Ragdoll ragdoll = PlaceholderRagdollBuilder.Build(new RagdollBuildSpec(simulation.Ragdoll, rules.Paths, segmentation, space,
                rules.Weapons, null));
            ragdoll.gameObject.SetActive(false);
            return ragdoll;
        }

        private static string Report(List<MatchStats> results, RulesSettings rules, SimulationSettings simulation, float hitStopSeconds,
            string profileName)
        {
            CultureInfo c = CultureInfo.InvariantCulture;
            var turns = results.Select(r => r.Turns).OrderBy(t => t).ToList();
            double meanTurns = turns.Average();
            double playbackPerTurn = results.Sum(r => r.ExecutedSeconds) / Math.Max(1, turns.Sum());
            double hitsPerMatch = results.Average(r => r.Hits);
            float planning = rules.Match.PlanningDurationSeconds;
            double playbackPerMatch = results.Average(r => r.ExecutedSeconds + r.Hits * hitStopSeconds);
            double upperBound = results.Average(r => r.Turns * planning + r.ExecutedSeconds + r.Hits * hitStopSeconds);

            var text = new StringBuilder();
            text.AppendLine($"X-RIM bot-vs-bot batch, {DateTime.Now.ToString("yyyy-MM-dd HH:mm", c)}");
            text.AppendLine($"{results.Count} matches, RandomBotBrain on both sides (seeds {FirstSeed}–{FirstSeed + Matches - 1}), tuning profile '{profileName}', " +
                            $"{simulation.Segmentation}, {simulation.StepRateHz.ToString("0", c)} Hz, turn cap {rules.Match.TurnCap}.");
            text.AppendLine("The random bot draws a random valid stroke every turn and never idles: a baseline for the loop, not a human.");
            text.AppendLine();
            text.AppendLine($"Ends: KO {Count(results, MatchEndReason.KnockOut)}, forfeit {Count(results, MatchEndReason.Forfeit)}, " +
                            $"sudden-death hit {Count(results, MatchEndReason.SuddenDeathHit)}, sudden-death tie stop {results.Count(r => r.StoppedOnTie)}, " +
                            $"unfinished at {TurnLimit} turns {results.Count(r => !r.Finished)}.");
            text.AppendLine($"Turns per match: mean {meanTurns.ToString("0.0", c)}, median {Median(turns).ToString("0.0", c)}, " +
                            $"min {turns.First()}, max {turns.Last()}. Sudden death reached in {results.Count(r => r.ReachedSuddenDeath)} of {results.Count}.");
            text.AppendLine($"Executed (played back) seconds per turn: mean {playbackPerTurn.ToString("0.00", c)}. " +
                            $"Hits per match: mean {hitsPerMatch.ToString("0.0", c)}; matches with a would-sever hit (dismemberment comes in Session 11; " +
                            $"target 10–20%): {results.Count(r => r.WouldSever)} of {results.Count}.");
            text.AppendLine($"Simulation cost per turn in the Editor: mean {results.SelectMany(r => r.SimulationMs).Average().ToString("0.0", c)} ms, " +
                            $"max {results.SelectMany(r => r.SimulationMs).Max().ToString("0.0", c)} ms.");
            text.AppendLine($"Match length for people (turns × (planning + playback + {hitStopSeconds.ToString("0.00", c)} s hitstop per hit)): " +
                            $"playback alone {Clock(playbackPerMatch)}; upper bound with the full {planning.ToString("0", c)} s of planning every turn {Clock(upperBound)}.");
            text.AppendLine();
            text.AppendLine("seed  turns  winner  reason          sudden  hits  played s");
            foreach (MatchStats r in results)
            {
                string reason = r.StoppedOnTie ? "tie stop" : r.Outcome != null ? r.Outcome.Reason.ToString() : "unfinished";
                string winner = r.Outcome != null ? r.Outcome.Winner.ToString() : "-";
                text.AppendLine(string.Format(c, "{0,4}  {1,5}  {2,-6}  {3,-14}  {4,-6}  {5,4}  {6,8:0.00}", r.Seed, r.Turns, winner, reason,
                    r.ReachedSuddenDeath ? "yes" : "no", r.Hits, r.ExecutedSeconds));
            }

            return text.ToString();
        }

        private static int Count(List<MatchStats> results, MatchEndReason reason) =>
            results.Count(r => r.Outcome != null && r.Outcome.Reason == reason);

        private static double Median(List<int> sorted) =>
            sorted.Count % 2 == 1 ? sorted[sorted.Count / 2] : (sorted[sorted.Count / 2 - 1] + sorted[sorted.Count / 2]) * 0.5;

        private static string Clock(double seconds)
        {
            int total = (int)Math.Round(seconds);
            return string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}", total / SecondsPerMinute, total % SecondsPerMinute);
        }

        private sealed class MatchStats
        {
            public MatchStats(uint seed)
            {
                Seed = seed;
            }

            public uint Seed { get; }
            public MatchOutcome Outcome { get; set; }
            public bool StoppedOnTie { get; set; }
            public bool Finished => Outcome != null || StoppedOnTie;
            public int Turns { get; private set; }
            public double ExecutedSeconds { get; private set; }
            public int Hits { get; private set; }
            public bool WouldSever { get; private set; }
            public bool ReachedSuddenDeath { get; private set; }
            public List<double> SimulationMs { get; } = new List<double>();

            public void Add(TurnResult turn, double simulationMs)
            {
                Turns++;
                ExecutedSeconds += turn.Timeline.Duration.Seconds;
                SimulationMs.Add(simulationMs);
                if (turn.FinalBoard.State.IsSuddenDeath) ReachedSuddenDeath = true;
                foreach (MatchEvent matchEvent in turn.Timeline.Events)
                {
                    if (!(matchEvent is HitLandedEvent hit)) continue;
                    Hits++;
                    if (hit.Damage.SeversLimb) WouldSever = true;
                }
            }
        }
    }
}
