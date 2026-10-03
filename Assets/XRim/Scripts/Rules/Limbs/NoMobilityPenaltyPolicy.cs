using XRim.Rules.Match;
using XRim.Rules.Planning;

namespace XRim.Rules.Limbs
{
    /// <summary>
    /// The §12 leg-loss penalty is TBD; until Session 11 designs it, losing a leg forbids no body move. Applied to every
    /// side's planning limits at the start of each turn, after the status effects.
    /// </summary>
    public sealed class NoMobilityPenaltyPolicy : IMobilityPenaltyPolicy
    {
        public void Apply(FighterState fighter, PlanningConstraints constraints)
        {
        }
    }
}
