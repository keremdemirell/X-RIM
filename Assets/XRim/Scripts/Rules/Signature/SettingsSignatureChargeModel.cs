using XRim.Core;
using XRim.Rules.Match;
using XRim.Rules.Settings;

namespace XRim.Rules.Signature
{
    /// <summary>
    /// The default charge model (GDD §8, TBD; designer 2026-10-15): the one <see cref="SignatureSettings.ChargeMode"/> names, read at
    /// every call, so switching the flag in the tuning panel switches the rule from the next match.
    /// </summary>
    public sealed class SettingsSignatureChargeModel : ISignatureChargeModel
    {
        private readonly OncePerMatchChargeModel _oncePerMatch = new OncePerMatchChargeModel();
        private readonly KineticEnergyBarChargeModel _bar = new KineticEnergyBarChargeModel();

        public bool IsAvailable(FighterState fighter, SignatureSettings settings) => For(settings).IsAvailable(fighter, settings);

        public float ChargeFraction(FighterState fighter, SignatureSettings settings) => For(settings).ChargeFraction(fighter, settings);

        public void OnDamageTaken(FighterState fighter, float hpDamage, SignatureSettings settings) =>
            For(settings).OnDamageTaken(fighter, hpDamage, settings);

        public void OnDamageDealt(FighterState fighter, float hpDamage, SignatureSettings settings) =>
            For(settings).OnDamageDealt(fighter, hpDamage, settings);

        public void OnUsed(FighterState fighter, SignatureSettings settings) => For(settings).OnUsed(fighter, settings);

        private ISignatureChargeModel For(SignatureSettings settings)
        {
            Guard.NotNull(settings, nameof(settings));
            return settings.ChargeMode == SignatureChargeMode.KineticEnergyBar ? _bar : (ISignatureChargeModel)_oncePerMatch;
        }
    }
}
