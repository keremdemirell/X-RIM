using XRim.Core;

namespace XRim.Rules.Match
{
    public sealed class MatchOutcome
    {
        public Side Winner { get; }
        public MatchEndReason Reason { get; }
        public int TurnCount { get; }

        public MatchOutcome(Side winner, MatchEndReason reason, int turnCount)
        {
            Winner = winner;
            Reason = reason;
            TurnCount = turnCount;
        }
    }
}
