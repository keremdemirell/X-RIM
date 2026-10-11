using XRim.Rules.Match;
using XRim.Rules.Planning;
using XRim.Rules.Settings;

namespace XRim.Rules.Limbs
{
    /// <summary>Losing a leg forbids no body move: the behaviour before Session 11, kept for comparison.</summary>
    public sealed class NoMobilityPenaltyPolicy : IMobilityPenaltyPolicy
    {
        public void Apply(FighterState fighter, PlanningConstraints constraints, RulesSettings settings)
        {
        }
    }
}
