using XRim.Core;
using XRim.Rules.Match;
using XRim.Rules.Settings;

namespace XRim.Rules.Signature
{
    /// <summary>
    /// GDD §8, option 1 (designer 2026-10-15, the default): simple and easy to read. A dummy may use
    /// <see cref="SignatureSettings.MovesPerMatch"/> signature moves in the match, available from the first turn; damage dealt and taken
    /// changes nothing.
    /// </summary>
    public sealed class OncePerMatchChargeModel : ISignatureChargeModel
    {
        public bool IsAvailable(FighterState fighter, SignatureSettings settings)
        {
            Guard.NotNull(fighter, nameof(fighter));
            Guard.NotNull(settings, nameof(settings));
            return fighter.SignatureUses < settings.MovesPerMatch;
        }

        public float ChargeFraction(FighterState fighter, SignatureSettings settings) => IsAvailable(fighter, settings) ? 1f : 0f;

        public void OnDamageTaken(FighterState fighter, float hpDamage, SignatureSettings settings)
        {
        }

        public void OnDamageDealt(FighterState fighter, float hpDamage, SignatureSettings settings)
        {
        }

        public void OnUsed(FighterState fighter, SignatureSettings settings)
        {
            Guard.NotNull(fighter, nameof(fighter));
            fighter.SignatureUses++;
        }
    }
}
