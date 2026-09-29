using XRim.Core;
using XRim.Core.Gdd;

namespace XRim.Rules.SuddenDeath
{
    /// <summary>
    /// Both dummies land hits in sudden death: the earlier time-to-impact wins (Decided, §14). This policy
    /// only handles two hits on the same simulation step.
    /// </summary>
    [GddTbd("§14", "Exact same-step tie", Proposal = "Replay the turn")]
    public interface ITiePolicy
    {
        SuddenDeathTieOutcome Resolve(SimTime leftImpact, SimTime rightImpact);
    }
}
