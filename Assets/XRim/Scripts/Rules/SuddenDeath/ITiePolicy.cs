using XRim.Core;
using XRim.Core.Gdd;

namespace XRim.Rules.SuddenDeath
{
    /// <summary>
    /// Both dummies' decisive moments in a sudden-death turn: the earlier time-to-impact wins (Decided, §14). This policy only handles
    /// moments at exactly the same instant (P4, designer 2026-10-11: times are measured inside a step to the microsecond, so two hits in
    /// the same simulation step are still ordered). Default: <see cref="ReplayTiePolicy"/>.
    /// </summary>
    [GddTbd("§14", "Exact same-step tie", Proposal = "Replay the turn (designer 2026-10-11); a replay counts toward the no-hit cap")]
    public interface ITiePolicy
    {
        SuddenDeathTieOutcome Resolve(SimTime leftImpact, SimTime rightImpact);
    }
}
