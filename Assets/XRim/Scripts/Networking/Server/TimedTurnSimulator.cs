using System;
using System.Diagnostics;
using XRim.Core;
using XRim.Simulation.Execution;
using XRim.Simulation.Recording;

namespace XRim.Networking.Server
{
    /// <summary>
    /// Passes every turn to the real simulator and says how long it took (GDD §18: the server pays the simulation the phone no longer does,
    /// Session 10 measured 58 ms mean, 100 ms worst on a Galaxy A32). Measured by a stopwatch outside the rules; it never changes a result.
    /// </summary>
    public sealed class TimedTurnSimulator : ITurnSimulator
    {
        /// <summary>How many of the latest turns the window statistics keep (the all-time numbers keep everything).</summary>
        private const int TimingWindowTurns = 1000;

        private readonly ITurnSimulator _inner;
        private readonly Stopwatch _watch = new Stopwatch();

        public TimedTurnSimulator(ITurnSimulator inner)
        {
            _inner = Guard.NotNull(inner, nameof(inner));
        }

        /// <summary>After each turn: its index and how many milliseconds it took.</summary>
        public event Action<int, double> Simulated;

        /// <summary>Each turn's time in milliseconds since the server started (the latest 1000 in the window, all of them in the all-time numbers).</summary>
        public RollingStatistics Timing { get; } = new RollingStatistics(TimingWindowTurns);

        public TurnResult Simulate(TurnInput input)
        {
            _watch.Restart();
            TurnResult result = _inner.Simulate(input);
            _watch.Stop();
            double milliseconds = _watch.Elapsed.TotalMilliseconds;
            Timing.Add(milliseconds);
            Simulated?.Invoke(result.TurnIndex, milliseconds);
            return result;
        }
    }
}
