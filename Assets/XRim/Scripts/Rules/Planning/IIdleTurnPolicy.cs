using XRim.Core.Gdd;

namespace XRim.Rules.Planning
{
    /// <summary>
    /// Decides whether a locked plan counts as an idle turn for the forfeit counter.
    /// The forfeit rule itself is Decided: <see cref="RuleConstants.IdleTurnsBeforeForfeit"/>.
    /// </summary>
    [GddTbd("§3", "Which inputs reset the forfeit counter", Proposal = "Any body move, drawn path or signature move")]
    public interface IIdleTurnPolicy
    {
        bool IsIdle(TurnPlan plan);
    }
}
