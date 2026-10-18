using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using XRim.Bots;
using XRim.Bots.Balance;
using XRim.Config;
using XRim.Editor.PlayModeJobs;
using XRim.Presentation.Feel;
using XRim.Rules.Settings;
using XRim.Simulation;
using XRim.Simulation.Recording;
using XRim.Simulation.Unity2D;

namespace XRim.Editor.Balance
{
    /// <summary>
    /// XRim → Balance → Run Bot Batch (Session 16): plays the <see cref="BalanceBatchSpec"/> on real physics with the project's tuning
    /// (a physics-only dummy built from it, <see cref="HeadlessPhysics"/>), times each turn on screen with the Feel asset, and writes the
    /// report and the settings it used to <c>Docs/balance/</c>. A batch cancelled or stopped for safety writes a partial report of the matches
    /// it finished.
    /// </summary>
    internal sealed class BotBatchJob : IPlayModeJob
    {
        private const string ReportFolderName = "balance";
        private const string DocsFolderName = "Docs";
        private const string FileStem = "bot-batch-";
        private const string TimestampFormat = "yyyy-MM-dd-HHmm";

        private readonly BalanceBatchSpec _spec;
        private readonly TuningProfile _tuning;
        private readonly RulesSettings _rules;
        private readonly SimulationSettings _simulation;
        private readonly BotSettings _bots;
        private readonly BalanceTargets _targets;
        private readonly HeadlessPhysics _physics;
        private readonly BalanceBatch _batch;
        private readonly DateTime _startedAt = DateTime.Now;

        public BotBatchJob(string arguments)
        {
            _spec = string.IsNullOrEmpty(arguments) ? new BalanceBatchSpec() : JsonUtility.FromJson<BalanceBatchSpec>(arguments);
            _tuning = AssetDatabase.LoadAssetAtPath<TuningProfile>(EditorPaths.TuningProfilePath);
            if (_tuning == null) throw new InvalidOperationException("No tuning profile: run XRim/Setup/Create Default Tuning Assets first.");

            var issues = new List<string>();
            _rules = _tuning.BuildRulesSettings(issues);
            _simulation = _tuning.BuildSimulationSettings(issues);
            _bots = _tuning.BuildBotSettings(issues);
            foreach (string issue in issues) Debug.LogWarning("[XRim] Bot batch: " + issue, _tuning);
            _targets = _tuning.BalanceTargets != null ? _tuning.BalanceTargets.CreateTargets() : DefaultTargets();

            FeelConfig feel = _tuning.Feel;
            Func<TurnResult, double> onScreen = null;
            if (feel != null)
            {
                HitStopSettings hitStops = HitStopSettings.From(feel);
                SlowMotionSettings slowMotion = SlowMotionSettings.From(feel);
                onScreen = turn => FeelDirector.PlannedRealSecondsOf(turn, hitStops, SlowMotionPlanner.Plan(turn, slowMotion));
            }

            _physics = new HeadlessPhysics(_rules, _simulation, _tuning.ArenaSpace);
            _batch = new BalanceBatch(_spec, () => _tuning.BuildRulesSettings(new List<string>()), _simulation, _bots, _physics.CreateSimulator(), onScreen);
        }

        public string Title => "Bot Batch";

        public string Status => _batch.Status;

        public float Progress => _batch.Progress;

        public bool Step() => _batch.Step();

        public string Finish(bool cancelled)
        {
            _batch.Dispose();
            _physics.Dispose();
            if (_batch.MatchesDone == 0) return "No match finished, so there is nothing to report.";

            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", DocsFolderName, ReportFolderName));
            Directory.CreateDirectory(folder);
            string stem = FileStem + _startedAt.ToString(TimestampFormat, CultureInfo.InvariantCulture);
            string reportPath = Path.Combine(folder, stem + ".md");
            string settingsPath = Path.Combine(folder, stem + "-settings.json");
            bool partial = !_batch.IsDone;

            File.WriteAllText(settingsPath, JsonUtility.ToJson(new SettingsRecord
            {
                Profile = _tuning.name, StartedAt = _startedAt.ToString("s", CultureInfo.InvariantCulture), Spec = _spec, Rules = _rules,
                Simulation = _simulation, Bots = _bots, AssumedPlanningSecondsPerTurn = _targets.AssumedPlanningSecondsPerTurn,
            }, true), new UTF8Encoding(false));
            string report = BalanceReport.Write(_batch.Results, _spec, _targets, _rules, Heading(Path.GetFileName(settingsPath)), partial);
            File.WriteAllText(reportPath, report, new UTF8Encoding(false));
            Debug.Log($"[XRim] Bot batch report written to {reportPath}");

            var message = new StringBuilder();
            message.AppendLine($"{_batch.MatchesDone} matches played{(partial ? " (partial)" : string.Empty)}. Report:");
            message.AppendLine($"Docs/{ReportFolderName}/{Path.GetFileName(reportPath)}");
            message.AppendLine($"(and the settings used: {Path.GetFileName(settingsPath)})");
            message.AppendLine();
            foreach (string line in Highlights())
            {
                message.AppendLine("• " + line);
            }

            return message.ToString();
        }

        private IEnumerable<string> Heading(string settingsFile)
        {
            CultureInfo c = CultureInfo.InvariantCulture;
            HeuristicBotSettings heuristic = _bots.Heuristic;
            yield return "Played " + _startedAt.ToString("yyyy-MM-dd HH:mm", c) + $", tuning profile '{_tuning.name}', settings snapshot `{settingsFile}`.";
            yield return string.Format(c, "Simulation: {0}, {1} Hz; turn cap {2}; planning {3:0.#} s; arm / leg durability as tuned {4:0.#} / {5:0.#}.",
                _simulation.Segmentation, _simulation.StepRateHz, _rules.Match.TurnCap, _rules.Match.PlanningDurationSeconds, _rules.Damage.ArmDurability,
                _rules.Damage.LegDurability);
            yield return string.Format(c, "{0} matches per run from seed {1}; loadouts {2}; brains swap sides every match.", _spec.MatchesPerRun, _spec.FirstSeed,
                _spec.RandomLoadouts ? "a random valid one per side per match (every weapon gets played)" : "the default");
            yield return string.Format(c, "Heuristic bot (Bots asset): aim error {0:0} units, mistakes {1:0%}, signature {2:0%}, duck {3:0%}, swing {4:0%}, " +
                "head {5:0%}, shield {6:0%}, retreat {7:0%}, jump {8:0%}.", heuristic.AimErrorUnits, heuristic.MistakeChance, heuristic.SignatureChance,
                heuristic.DuckChance, heuristic.SwingChance, heuristic.HeadAimChance, heuristic.ShieldGuardChance, heuristic.RetreatChance, heuristic.JumpChance);
            yield return "The random bot draws a random valid stroke every turn; neither bot is a person (see How to read this).";
        }

        /// <summary>The Balance Targets asset's own defaults, for a profile made before it had one.</summary>
        private static BalanceTargets DefaultTargets()
        {
            var defaults = ScriptableObject.CreateInstance<BalanceTargetsConfig>();
            try
            {
                return defaults.CreateTargets();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(defaults);
            }
        }

        /// <summary>A few numbers for the closing dialog; the report has them all.</summary>
        private IEnumerable<string> Highlights()
        {
            CultureInfo c = CultureInfo.InvariantCulture;
            foreach (BalanceRunResult result in _batch.Results.Where(r => r.Run.IsBaseline && r.Records.Count > 0))
            {
                int n = result.Records.Count;
                string wins = result.Run.Matchup.IsMirror
                    ? string.Empty
                    : string.Format(c, "{0} won {1:0%}; ", result.Run.Matchup.First, (double)result.Records.Count(r => r.WinningBrain == result.Run.Matchup.First) / n);
                yield return string.Format(c, "{0}: {1}severs in {2:0%} of matches (target {3:0%}–{4:0%}); {5:0.0} turns on average.", result.Run.Label, wins,
                    (double)result.Records.Count(r => r.Severs > 0) / n, _targets.DismembermentMatchRateMin, _targets.DismembermentMatchRateMax,
                    result.Records.Average(r => r.Turns));
            }
        }

        /// <summary>Everything the batch played with, saved next to the report so a run can be repeated.</summary>
        [Serializable]
        private sealed class SettingsRecord
        {
            public string Profile;
            public string StartedAt;
            public BalanceBatchSpec Spec;
            public RulesSettings Rules;
            public SimulationSettings Simulation;
            public BotSettings Bots;
            public float AssumedPlanningSecondsPerTurn;
        }
    }
}
