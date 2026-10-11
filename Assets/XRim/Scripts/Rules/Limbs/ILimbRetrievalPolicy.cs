using XRim.Core.Gdd;
using XRim.Rules.Match;
using XRim.Rules.Settings;

namespace XRim.Rules.Limbs
{
    /// <summary>
    /// Whether a severed limb on the floor can be picked up as a club this turn (GDD §12). Asked by the simulation at the start of
    /// the turn, with the distance from the wielder's shoulder to the limb.
    /// </summary>
    [GddTbd("§12", "Retrieving a severed limb", Proposal = "Automatic on selection; out-of-reach case open")]
    public interface ILimbRetrievalPolicy
    {
        bool CanRetrieve(FighterState fighter, float distanceToLimbUnits, RulesSettings settings);
    }
}
