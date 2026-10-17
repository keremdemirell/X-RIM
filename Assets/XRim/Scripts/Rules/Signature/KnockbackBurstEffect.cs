using XRim.Core;
using XRim.Rules.Settings;

namespace XRim.Rules.Signature
{
    /// <summary>
    /// Placeholder special property (GDD §8; designer 2026-10-15, "5 Finger Exploding Touch"): each hit the move lands knocks the victim back
    /// <see cref="SignatureSettings.KnockbackBurstMultiplier"/> times as far (E1: the victim stays where it lands).
    /// </summary>
    public sealed class KnockbackBurstEffect : ISignatureEffect
    {
        public string Id => SignatureEffectIds.KnockbackBurst;

        public float DamageMultiplier(SignatureSettings settings) => 1f;

        public float KnockbackMultiplier(SignatureSettings settings) => Guard.NotNull(settings, nameof(settings)).KnockbackBurstMultiplier;

        public bool StunsOnHit(SignatureSettings settings) => false;
    }
}
