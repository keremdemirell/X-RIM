using System;
using System.Diagnostics;
using XRim.Core;
using XRim.Simulation;
using XRim.Simulation.Execution;
using XRim.Simulation.Recording;

namespace XRim.App
{
    /// <summary>
    /// Passes every turn to the real simulator and remembers the last input and how long it took, so debug tools can show
    /// the cost and re-simulate the last turn with the current tuning (a turn is a pure function of its input,
    /// ARCHITECTURE §1).
    /// </summary>
    public sealed class RecordingTurnSimulator : ITurnSimulator
    {
        private readonly ITurnSimulator _inner;
        private readonly Stopwatch _watch = new Stopwatch();

        public RecordingTurnSimulator(ITurnSimulator inner)
        {
            _inner = Guard.NotNull(inner, nameof(inner));
        }

        /// <summary>The last turn's board, plans and settings; null before the first turn.</summary>
        public TurnInput LastInput { get; private set; }

        /// <summary>Wall-clock time the last simulation took (debug: device cost of a turn).</summary>
        public double LastSimulationMilliseconds { get; private set; }

        /// <summary>
        /// Debug sandbox: when set, every turn runs with these simulation settings instead of the match's snapshot, so
        /// simulation tuning applies from the next turn. Rules settings always stay the match's.
        /// </summary>
        public Func<SimulationSettings> SimulationSettingsOverride { get; set; }

        public TurnResult Simulate(TurnInput input)
        {
            Guard.NotNull(input, nameof(input));
            if (SimulationSettingsOverride != null) input = new TurnInput(input.Board, input.Plans, input.Rules, SimulationSettingsOverride());
            LastInput = input;
            _watch.Restart();
            TurnResult result = _inner.Simulate(input);
            _watch.Stop();
            LastSimulationMilliseconds = _watch.Elapsed.TotalMilliseconds;
            return result;
        }
    }
}
