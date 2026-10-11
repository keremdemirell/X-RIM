using XRim.Core;
using XRim.Core.Gdd;

namespace XRim.Rules.SuddenDeath
{
    /// <summary>
    /// The designer's default (2026-10-11, GDD §14 TBD): a sudden-death turn whose decisive moments came at exactly the same instant is
    /// replayed, both back at 1 HP. A replay counts toward the no-hit cap (P6), so ties can never loop for ever.
    /// </summary>
    [GddTbd("§14", "Exact same-step tie", Proposal = "Replay the turn; designer 2026-10-11: the recommended default")]
    public sealed class ReplayTiePolicy : ITiePolicy
    {
        public SuddenDeathTieOutcome Resolve(SimTime leftImpact, SimTime rightImpact) => SuddenDeathTieOutcome.ReplayTurn;
    }
}
