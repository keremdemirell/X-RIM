using XRim.Core;
using XRim.Rules.Match;
using XRim.Rules.Paths;
using XRim.Rules.Settings;

namespace XRim.Rules.Limbs
{
    /// <summary>
    /// The retrieval rule <see cref="LimbLossSettings.Retrieval"/> names (GDD §12, TBD). The default (designer, 2026-10-10): picking
    /// the club retrieves a limb automatically, wherever it lies. The other option keeps to the club's reach.
    /// </summary>
    public sealed class SettingsLimbRetrievalPolicy : ILimbRetrievalPolicy
    {
        public bool CanRetrieve(FighterState fighter, float distanceToLimbUnits, RulesSettings settings)
        {
            Guard.NotNull(settings, nameof(settings));
            if (settings.Damage.LimbLoss.Retrieval == LimbRetrievalRule.Automatic) return true;

            WeaponStats club = settings.FindWeapon(WeaponIds.SeveredLimb);
            return club != null && distanceToLimbUnits <= ReachLimit.For(settings.Paths, club).RadiusUnits;
        }
    }
}
