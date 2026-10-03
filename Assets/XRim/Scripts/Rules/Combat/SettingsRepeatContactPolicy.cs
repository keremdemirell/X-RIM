using XRim.Core;
using XRim.Rules.Settings;

namespace XRim.Rules.Combat
{
    /// <summary>
    /// The default <see cref="IRepeatContactPolicy"/>: the rules resolve up to <see cref="ClashSettings.MaxResolvedContactsPerWeaponPair"/>
    /// contacts between the two held items per turn (D19 default: 1, so two blades grinding together never loop); 0 resolves
    /// every contact.
    /// </summary>
    public sealed class SettingsRepeatContactPolicy : IRepeatContactPolicy
    {
        public bool ShouldResolve(int earlierResolvedContacts, ClashSettings settings)
        {
            int max = Guard.NotNull(settings, nameof(settings)).MaxResolvedContactsPerWeaponPair;
            return max <= 0 || earlierResolvedContacts < max;
        }
    }
}
