using XRim.Core.Gdd;
using XRim.Rules.Match;
using XRim.Rules.Planning;

namespace XRim.Rules.Limbs
{
    /// <summary>
    /// How leg loss limits body moves. That a severed leg can no longer be used is Decided (§12);
    /// the exact penalty and the both-legs case are not.
    /// </summary>
    [GddTbd("§12", "Effect of losing one leg, and both legs", Proposal = "No jump/lunge, crawl, or shorter steps")]
    [GddTbd("§5", "How each move changes after losing a leg")]
    public interface IMobilityPenaltyPolicy
    {
        void Apply(FighterState fighter, PlanningConstraints constraints);
    }
}
