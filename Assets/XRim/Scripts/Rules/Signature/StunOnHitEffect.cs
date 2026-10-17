using XRim.Rules.Settings;

namespace XRim.Rules.Signature
{
    /// <summary>
    /// Placeholder special property (GDD §8; designer 2026-10-15, "Head Impact Simulation #404"): every hit the move lands stuns the victim,
    /// whatever the zone and the damage. The stun is the D17 effect the rules already give a strong head hit (no body move next turn by
    /// default); a hit that kills does not stun, as ever.
    /// </summary>
    public sealed class StunOnHitEffect : ISignatureEffect
    {
        public string Id => SignatureEffectIds.StunOnHit;

        public float DamageMultiplier(SignatureSettings settings) => 1f;

        public float KnockbackMultiplier(SignatureSettings settings) => 1f;

        public bool StunsOnHit(SignatureSettings settings) => true;
    }
}
