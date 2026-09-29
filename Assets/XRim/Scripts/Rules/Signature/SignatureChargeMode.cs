using XRim.Core.Gdd;

namespace XRim.Rules.Signature
{
    [GddTbd("§8", "Signature charge: once per match or Kinetic Energy Bar")]
    public enum SignatureChargeMode
    {
        /// <summary>Simple and easy to read.</summary>
        OncePerMatch = 0,

        /// <summary>Filled by damage dealt and taken; comeback swings, needs more tuning.</summary>
        KineticEnergyBar = 1,
    }
}
