using System;
using XRim.Core.Gdd;
using XRim.Rules.Signature;

namespace XRim.Rules.Settings
{
    /// <summary>Signature (joker) move rules (GDD §8). Almost everything here is TBD.</summary>
    [Serializable]
    public sealed class SignatureSettings
    {
        [GddTbd("§8", "Signature charge: once per match or Kinetic Energy Bar")]
        public SignatureChargeMode ChargeMode = SignatureChargeMode.OncePerMatch;

        [GddTbd("§8", "Number of signature moves per match")]
        [Placeholder("§8 count per match is TBD")]
        public int MovesPerMatch = 1;

        [GddTbd("§8", "Can the opponent see a charged signature move?")]
        public bool OpponentSeesCharged = false;
    }
}
