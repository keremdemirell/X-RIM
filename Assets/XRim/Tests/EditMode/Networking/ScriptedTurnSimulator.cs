using System;
using System.Collections.Generic;
using XRim.Core;
using XRim.Rules.Match;
using XRim.Simulation.Execution;
using XRim.Simulation.Physics;
using XRim.Simulation.Recording;

namespace XRim.Tests.EditMode.Networking
{
    /// <summary>
    /// A stand-in for the physics simulation: remembers every <see cref="TurnInput"/> and lets a test script what
    /// happened in each turn (for example damage) by editing the resolved match state. With no script, nothing happens.
    /// </summary>
    internal sealed class ScriptedTurnSimulator : ITurnSimulator
    {
        private readonly Action<int, MatchState> _script;
        private readonly double _timelineSeconds;

        public List<TurnInput> Inputs { get; } = new List<TurnInput>();

        /// <param name="script">Called with the turn index and the state to change.</param>
        /// <param name="timelineSeconds">Duration of the recorded timeline; 0 records no frames at all.</param>
        public ScriptedTurnSimulator(Action<int, MatchState> script = null, double timelineSeconds = 0.0)
        {
            _script = script;
            _timelineSeconds = timelineSeconds;
        }

        public TurnResult Simulate(TurnInput input)
        {
            Inputs.Add(input);
            BoardSnapshot board = input.Board.Clone();
            _script?.Invoke(board.State.TurnIndex, board.State);

            var recorder = new TimelineRecorder();
            if (_timelineSeconds > 0.0)
            {
                recorder.RecordFrame(0, SimTime.Zero, new PoseSnapshot());
                recorder.RecordFrame(1, SimTime.FromSeconds(_timelineSeconds), new PoseSnapshot());
            }

            var report = new ExecutionReport(board.State, new PerSide<SimTime?>(null, null), false);
            return new TurnResult(board.State.TurnIndex, recorder.Build(), board, report);
        }
    }
}
