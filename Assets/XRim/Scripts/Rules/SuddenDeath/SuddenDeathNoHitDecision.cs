using XRim.Core;
using XRim.Rules.Match;

namespace XRim.Rules.SuddenDeath
{
    /// <summary>What <see cref="ISuddenDeathNoHitPolicy"/> decides after a sudden-death turn without a winner: play another, or who wins and why.</summary>
    public readonly struct SuddenDeathNoHitDecision
    {
        public bool RepeatsTurn { get; }

        /// <summary>Meaningful only when the turn is not repeated.</summary>
        public Side Winner { get; }

        /// <summary>Meaningful only when the turn is not repeated.</summary>
        public MatchEndReason Reason { get; }

        private SuddenDeathNoHitDecision(bool repeatsTurn, Side winner, MatchEndReason reason)
        {
            RepeatsTurn = repeatsTurn;
            Winner = winner;
            Reason = reason;
        }

        public static SuddenDeathNoHitDecision Repeat => new SuddenDeathNoHitDecision(true, default, default);

        public static SuddenDeathNoHitDecision Win(Side winner, MatchEndReason reason) => new SuddenDeathNoHitDecision(false, winner, reason);
    }
}
