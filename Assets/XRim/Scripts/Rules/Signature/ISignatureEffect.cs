using XRim.Core.Gdd;
using XRim.Rules.Settings;

namespace XRim.Rules.Signature
{
    /// <summary>
    /// A signature move's special property (GDD §8, Decided: "each signature move is a preset pattern with its own special property"), by
    /// id (<see cref="SignatureMoveStats.SpecialEffectId"/>). It holds no state and acts only through the rules' existing seams: a factor
    /// on the damage of the hits the move's own swing lands, a factor on how far those hits knock the victim back (E1), and the D17 stun.
    /// Its numbers live in <see cref="SignatureSettings"/>, never here. The catalogue is TBD; designer 2026-10-15: three placeholders.
    /// </summary>
    [GddTbd("§8", "Catalogue of signature moves", Proposal = "Three placeholders: knockback burst, stun on hit, damage boost (designer 2026-10-15)")]
    public interface ISignatureEffect
    {
        string Id { get; }

        /// <summary>The factor on the damage of each hit the move lands (1: unchanged).</summary>
        float DamageMultiplier(SignatureSettings settings);

        /// <summary>The factor on how far each hit the move lands knocks the victim back (1: unchanged).</summary>
        float KnockbackMultiplier(SignatureSettings settings);

        /// <summary>Every hit the move lands stuns the victim (the D17 effect), whatever the zone and damage.</summary>
        bool StunsOnHit(SignatureSettings settings);
    }
}
