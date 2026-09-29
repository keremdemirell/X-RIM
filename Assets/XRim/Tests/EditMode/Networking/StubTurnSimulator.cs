using XRim.Core;
using XRim.Rules.Match;
using XRim.Simulation.Execution;
using XRim.Simulation.Recording;

namespace XRim.Tests.EditMode.Networking
{
    /// <summary>Resolves every turn as "nothing happened": same board, no events.</summary>
    internal sealed class StubTurnSimulator : ITurnSimulator
    {
        public int CallCount { get; private set; }

        public TurnResult Simulate(TurnInput input)
        {
            CallCount++;
            BoardSnapshot board = input.Board.Clone();
            var report = new ExecutionReport(board.State, new PerSide<SimTime?>(null, null), false);
            var timeline = new TimelineRecorder().Build();
            return new TurnResult(board.State.TurnIndex, timeline, board, report);
        }
    }
}
