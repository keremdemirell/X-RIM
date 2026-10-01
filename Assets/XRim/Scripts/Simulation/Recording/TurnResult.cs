using XRim.Core;
using XRim.Rules.Match;

namespace XRim.Simulation.Recording
{
    /// <summary>
    /// The output of resolving one turn, identical whether it was produced locally or by a server.
    /// Clients only ever play this back.
    /// </summary>
    public sealed class TurnResult
    {
        public int TurnIndex { get; }
        public TurnTimeline Timeline { get; }

        /// <summary>The new frozen board for the next planning phase.</summary>
        public BoardSnapshot FinalBoard { get; }

        /// <summary>What the rules state machine needs to finish the turn.</summary>
        public ExecutionReport Report { get; }

        /// <summary>Whether the turn settled or hit the execution hard cap (debug tools show it).</summary>
        public TurnEndReason EndReason { get; }

        public TurnResult(int turnIndex, TurnTimeline timeline, BoardSnapshot finalBoard, ExecutionReport report,
            TurnEndReason endReason = TurnEndReason.Settled)
        {
            TurnIndex = turnIndex;
            Timeline = Guard.NotNull(timeline, nameof(timeline));
            FinalBoard = Guard.NotNull(finalBoard, nameof(finalBoard));
            Report = Guard.NotNull(report, nameof(report));
            EndReason = endReason;
        }
    }
}
