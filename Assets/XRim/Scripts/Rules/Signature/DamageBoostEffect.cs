using XRim.Core;
using XRim.Rules.Settings;

namespace XRim.Rules.Signature
{
    /// <summary>
    /// Placeholder special property (GDD §8; designer 2026-10-15, "Whiplash Calibration Spin"): each hit the move lands deals
    /// <see cref="SignatureSettings.DamageBoostMultiplier"/> times the damage, through the damage formula's "Modifiers" (§11), so the
    /// no-instant-KO rule (D27) and the limb cap still apply after it.
    /// </summary>
    public sealed class DamageBoostEffect : ISignatureEffect
    {
        public string Id => SignatureEffectIds.DamageBoost;

        public float DamageMultiplier(SignatureSettings settings) => Guard.NotNull(settings, nameof(settings)).DamageBoostMultiplier;

        public float KnockbackMultiplier(SignatureSettings settings) => 1f;

        public bool StunsOnHit(SignatureSettings settings) => false;
    }
}
