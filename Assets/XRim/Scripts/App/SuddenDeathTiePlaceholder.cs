using XRim.Core;
using XRim.Core.Gdd;
using XRim.Rules.Match;
using XRim.Simulation.Recording;

namespace XRim.App
{
    /// <summary>
    /// The one sudden-death case the current rules cannot finish: both dummies fall at the same instant in a sudden-death turn. The
    /// rules then repeat the turn with both at 0 HP, forever, until Session 12 decides the tie (§14, TBD; the marked seam in
    /// <see cref="EndConditionEvaluator"/>). Until then the first playable stops the match there with a placeholder screen
    /// (designer, 2026-10-04). Every other sudden-death turn plays on: 1 HP each, the first hit wins.
    /// </summary>
    [GddTbd("§14", "Exact tie in sudden death", Proposal = "Session 12 decides (default: replay); the first playable stops the match here")]
    public static class SuddenDeathTiePlaceholder
    {
        /// <summary>True when this turn was a sudden-death turn that both dummies ended dead.</summary>
        public static bool StopsTheMatch(TurnResult turn)
        {
            if (turn == null) return false;
            MatchState state = turn.Report.ResolvedState;
            return state.IsSuddenDeath && state.Fighters[Side.Left].IsDead && state.Fighters[Side.Right].IsDead;
        }
    }
}
