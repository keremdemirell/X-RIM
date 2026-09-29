using XRim.Core;
using XRim.Core.Gdd;

namespace XRim.Rules.Signature
{
    /// <summary>When a signature move is available. Signature moves are unlocked with earned currency only (§8, §16).</summary>
    [GddTbd("§8", "Signature charge: once per match or Kinetic Energy Bar")]
    [GddTbd("§8", "Number of signature moves per match")]
    public interface ISignatureChargeModel
    {
        bool IsAvailable(Side side);

        void OnDamageDealt(Side side, float hpDamage);

        void OnDamageTaken(Side side, float hpDamage);

        void OnUsed(Side side);
    }
}
