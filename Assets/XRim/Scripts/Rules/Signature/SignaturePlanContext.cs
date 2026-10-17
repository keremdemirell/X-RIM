using System.Collections.Generic;
using XRim.Core;
using XRim.Rules.Planning;
using XRim.Rules.Settings;

namespace XRim.Rules.Signature
{
    /// <summary>
    /// What <see cref="PlanValidator"/> needs to judge a plan that holds a signature move (GDD §8): the dummy's charge as planning
    /// opened, the moves it brought, and where each weapon's tip rests (the preset's lead-in starts there). Without one, a validator
    /// refuses every signature plan.
    /// </summary>
    public sealed class SignaturePlanContext
    {
        public SignatureChargeState Charge { get; }
        public IReadOnlyList<SignatureMoveStats> Moves { get; }
        public IWeaponTipSource WeaponTips { get; }

        public SignaturePlanContext(SignatureChargeState charge, IReadOnlyList<SignatureMoveStats> moves, IWeaponTipSource weaponTips)
        {
            Charge = charge;
            Moves = Guard.NotNull(moves, nameof(moves));
            WeaponTips = Guard.NotNull(weaponTips, nameof(weaponTips));
        }
    }
}
