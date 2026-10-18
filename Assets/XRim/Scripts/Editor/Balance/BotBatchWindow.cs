using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEngine;
using XRim.Bots;
using XRim.Bots.Balance;
using XRim.Editor.PlayModeJobs;

namespace XRim.Editor.Balance
{
    /// <summary>
    /// XRim → Balance → Run Bot Batch… (Session 16): picks what the bot-vs-bot balance batch plays (matches per run, the first seed, random
    /// loadouts, which matchups, the limb-durability sweep), shows roughly how long it takes, and starts it. It runs in Play mode by itself
    /// (<see cref="PlayModeJobRunner"/>, with the 10 GB disk check and the 30-minute limit) and writes its report to <c>Docs/balance/</c>.
    /// </summary>
    internal sealed class BotBatchWindow : EditorWindow
    {
        /// <summary>Roughly what one bot match costs in the Editor (Session 09: about 33 turns at 15 ms, plus the loads): for the estimate only.</summary>
        private const float EstimatedSecondsPerMatch = 0.7f;

        private const float SecondsPerMinute = 60f;
        private const int MinMatchesPerRun = 2;
        private const int MaxMatchesPerRun = 1000;

        /// <summary>A batch estimated past this many minutes gets a warning: the runner stops every job after 30.</summary>
        private const float LongBatchMinutes = 25f;

        private const float WindowWidth = 460f;
        private const float WindowHeight = 380f;

        private int _matchesPerRun = BalanceBatchSpec.DefaultMatchesPerRun;
        private int _firstSeed = 1;
        private bool _randomLoadouts = true;
        private bool _heuristicVsRandom = true;
        private bool _heuristicVsHeuristic = true;
        private bool _randomVsRandom;
        private bool _sweep = true;
        private string _factors = "0.6, 0.8, 1.25";

        [MenuItem(EditorPaths.MenuBalance + "Run Bot Batch…")]
        private static void Open()
        {
            var window = GetWindow<BotBatchWindow>(true, "X-RIM Bot Batch");
            window.minSize = new Vector2(WindowWidth, WindowHeight);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox("Bots play each other on real physics with the project's tuning and a report goes to Docs/balance/ (PT6: " +
                                    "dismemberment rate, match length, weapon pick and win rates, sudden death, wall zaps). Unity enters Play mode by " +
                                    "itself and comes back when it is done; Cancel on the progress bar stops it.", MessageType.Info);
            EditorGUILayout.Space();
            _matchesPerRun = Mathf.Clamp(EditorGUILayout.IntField("Matches per run", _matchesPerRun), MinMatchesPerRun, MaxMatchesPerRun);
            _firstSeed = Mathf.Max(1, EditorGUILayout.IntField("First seed", _firstSeed));
            _randomLoadouts = EditorGUILayout.Toggle(new GUIContent("Random loadouts", "Each bot picks a random valid loadout every match, so every weapon is played."),
                _randomLoadouts);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Matchups (the brains swap sides every match)", EditorStyles.boldLabel);
            _heuristicVsRandom = EditorGUILayout.Toggle("Heuristic vs random", _heuristicVsRandom);
            _heuristicVsHeuristic = EditorGUILayout.Toggle("Heuristic vs heuristic", _heuristicVsHeuristic);
            _randomVsRandom = EditorGUILayout.Toggle("Random vs random", _randomVsRandom);

            EditorGUILayout.Space();
            _sweep = EditorGUILayout.Toggle(new GUIContent("Limb durability sweep", "Plays heuristic vs heuristic again with arm and leg durability scaled by each " +
                                                                                    "factor, to see which comes nearest the GDD §11 target (severs in 10–20% of matches)."), _sweep);
            using (new EditorGUI.DisabledScope(!_sweep))
            {
                _factors = EditorGUILayout.TextField("Factors", _factors);
            }

            BalanceBatchSpec spec = Spec();
            EditorGUILayout.Space();
            float minutes = spec.TotalMatches * EstimatedSecondsPerMatch / SecondsPerMinute;
            EditorGUILayout.LabelField(string.Format(CultureInfo.InvariantCulture, "{0} runs × {1} matches = {2} matches, roughly {3:0.#} minutes.",
                spec.Runs().Count, spec.MatchesPerRun, spec.TotalMatches, minutes));
            if (minutes > LongBatchMinutes)
                EditorGUILayout.HelpBox("Every job stops itself after 30 minutes (a partial report is still written). Fewer matches or runs fit.", MessageType.Warning);

            using (new EditorGUI.DisabledScope(spec.TotalMatches == 0))
            {
                if (GUILayout.Button("Run the batch")) PlayModeJobRunner.Start(PlayModeJobKinds.BotBatch, JsonUtility.ToJson(spec));
            }
        }

        private BalanceBatchSpec Spec()
        {
            var matchups = new List<BalanceMatchup>();
            if (_heuristicVsRandom) matchups.Add(new BalanceMatchup(BotBrainKind.Heuristic, BotBrainKind.Random));
            if (_heuristicVsHeuristic) matchups.Add(new BalanceMatchup(BotBrainKind.Heuristic, BotBrainKind.Heuristic));
            if (_randomVsRandom) matchups.Add(new BalanceMatchup(BotBrainKind.Random, BotBrainKind.Random));
            return new BalanceBatchSpec
            {
                MatchesPerRun = _matchesPerRun, FirstSeed = _firstSeed, RandomLoadouts = _randomLoadouts, Matchups = matchups, SweepDurability = _sweep,
                DurabilityFactors = ParseFactors(_factors),
            };
        }

        private static List<float> ParseFactors(string text) =>
            (text ?? string.Empty).Split(',', ';', ' ')
                .Select(part => float.TryParse(part.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float value) ? value : 0f)
                .Where(value => value > 0f).Distinct().ToList();
    }
}
