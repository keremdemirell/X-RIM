using XRim.Core.Gdd;
using XRim.Rules.Combat;
using XRim.Rules.Match;
using XRim.Rules.Settings;

namespace XRim.Rules.Damage
{
    /// <summary>
    /// How the Decided rule "a single head hit can never cause an instant KO" (GDD §11) is enforced. The GDD does not say
    /// how (D27).
    /// </summary>
    [GddTbd("§11", "How a single head hit is kept from causing an instant KO (not covered by the GDD)",
        Proposal = "D27 default (designer, 2026-10-03): a single hit cannot take a dummy from full HP to 0")]
    public interface IInstantKoPolicy
    {
        /// <summary>The HP a hit actually removes, given its damage and the victim before the hit.</summary>
        float LimitHpDamage(float damage, HitFacts hit, FighterState victim, DamageSettings settings);
    }
}
