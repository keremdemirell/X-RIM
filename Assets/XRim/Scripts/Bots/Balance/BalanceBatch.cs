using System;
using System.Collections.Generic;
using System.Globalization;
using XRim.Core;
using XRim.Rules.Settings;
using XRim.Simulation;
using XRim.Simulation.Execution;
using XRim.Simulation.Recording;

namespace XRim.Bots.Balance
{
    /// <summary>
    /// The whole bot-vs-bot balance batch (Session 16), a turn at a time (<see cref="Step"/>), so an Editor job or a test can spread it over
    /// frames: every run of the <see cref="BalanceBatchSpec"/>, each with its own fresh rules snapshot (limb durability scaled by the run's
    /// factor), matches seeded from the spec's first seed with the brains swapping sides every other match. Engine-free: it plays on whatever
    /// simulator it is given (the real physics in the Editor, a scripted one in tests).
    /// </summary>
    public sealed class BalanceBatch : IDisposable
    {
        private readonly BalanceBatchSpec _spec;
        private readonly Func<RulesSettings> _createRules;
        private readonly SimulationSettings _simulation;
        private readonly BotSettings _bots;
        private readonly ITurnSimulator _simulator;
        private readonly Func<TurnResult, double> _onScreenSeconds;
        private readonly List<BalanceRun> _runs;
        private readonly List<BalanceRunResult> _results = new List<BalanceRunResult>();
        private RulesSettings _runRules;
        private BotMatchRun _current;
        private int _runIndex;
        private int _matchIndex;

        /// <param name="createRules">A fresh rules snapshot for each run (the run scales its limb durability).</param>
        /// <param name="onScreenSeconds">A turn's time on screen with the feel layer; null counts its recorded length.</param>
        public BalanceBatch(BalanceBatchSpec spec, Func<RulesSettings> createRules, SimulationSettings simulation, BotSettings bots,
            ITurnSimulator simulator, Func<TurnResult, double> onScreenSeconds = null)
        {
            _spec = Guard.NotNull(spec, nameof(spec));
            _createRules = Guard.NotNull(createRules, nameof(createRules));
            _simulation = Guard.NotNull(simulation, nameof(simulation));
            _bots = Guard.NotNull(bots, nameof(bots));
            _simulator = Guard.NotNull(simulator, nameof(simulator));
            _onScreenSeconds = onScreenSeconds;
            _runs = spec.MatchesPerRun > 0 ? spec.Runs() : new List<BalanceRun>();
            TotalMatches = _runs.Count * Math.Max(spec.MatchesPerRun, 0);
        }

        public IReadOnlyList<BalanceRunResult> Results => _results;
        public int TotalMatches { get; }
        public int MatchesDone { get; private set; }
        public bool IsDone => _runIndex >= _runs.Count;
        public float Progress => TotalMatches == 0 ? 1f : (float)MatchesDone / TotalMatches;

        public string Status => IsDone
            ? "Done"
            : string.Format(CultureInfo.InvariantCulture, "{0}: match {1} of {2} ({3} of {4} in all)", _runs[_runIndex].Label, _matchIndex + 1,
                _spec.MatchesPerRun, MatchesDone + 1, TotalMatches);

        /// <summary>Plays one turn; false once every run is done.</summary>
        public bool Step()
        {
            if (IsDone) return false;
            if (_current == null) StartMatch();
            _current.Step();
            if (!_current.IsOver) return true;

            _results[_runIndex].Add(_current.Record);
            _current.Dispose();
            _current = null;
            MatchesDone++;
            if (++_matchIndex >= _spec.MatchesPerRun)
            {
                _runIndex++;
                _matchIndex = 0;
            }

            return !IsDone;
        }

        public void Dispose()
        {
            _current?.Dispose();
            _current = null;
        }

        private void StartMatch()
        {
            BalanceRun run = _runs[_runIndex];
            if (_matchIndex == 0)
            {
                _runRules = _createRules();
                _runRules.Damage.ArmDurability *= run.DurabilityFactor;
                _runRules.Damage.LegDurability *= run.DurabilityFactor;
                _results.Add(new BalanceRunResult(run, _runRules.Damage.ArmDurability, _runRules.Damage.LegDurability));
            }

            int seed = _spec.FirstSeed + _matchIndex;
            _current = new BotMatchRun(seed, run, _matchIndex % 2 == 0, _runRules, _simulation, _bots, _simulator, _spec.RandomLoadouts,
                _spec.TurnLimit, _onScreenSeconds);
        }
    }
}
