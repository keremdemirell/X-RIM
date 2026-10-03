using XRim.Core.Gdd;
using XRim.Rules.Settings;

namespace XRim.Rules.Combat
{
    /// <summary>
    /// Whether another contact between the same two held items in one turn is resolved again (GDD §10 open point, D19).
    /// Weapon-to-weapon and weapon-to-shield contacts share the count. A contact the rules do not resolve passes through:
    /// physics only.
    /// </summary>
    [GddTbd("§10", "Multiple contacts between two weapons in one turn",
        Proposal = "D19 default (designer, 2026-10-03): only the first contact resolves; later ones pass through")]
    public interface IRepeatContactPolicy
    {
        /// <param name="earlierResolvedContacts">Contacts between the pair the rules already resolved this turn.</param>
        bool ShouldResolve(int earlierResolvedContacts, ClashSettings settings);
    }
}
