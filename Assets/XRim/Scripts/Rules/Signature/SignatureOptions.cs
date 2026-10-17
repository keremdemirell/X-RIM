using System.Collections.Generic;
using XRim.Core;
using XRim.Rules.Planning;
using XRim.Rules.Settings;

namespace XRim.Rules.Signature
{
    /// <summary>
    /// What one player may do with signature moves this planning phase (GDD §8), as the authority's session decided it: the moves they
    /// brought, their charge, and whether a move can be used at all this turn (charged, brought, and allowed: sudden death can switch
    /// moves off, §14). The player's HUD shows it; only that player may see it (the opponent sees a charge only through the public
    /// state, when the flag allows it).
    /// </summary>
    public sealed class SignatureOptions
    {
        private static readonly SignatureMoveStats[] NoMoves = new SignatureMoveStats[0];

        /// <summary>No moves, not charged.</summary>
        public static SignatureOptions None { get; } = new SignatureOptions(NoMoves, default, false);

        public IReadOnlyList<SignatureMoveStats> Moves { get; }
        public SignatureChargeState Charge { get; }

        /// <summary>A move can be used this turn (the weapon tie and the drawing lock-out are checked per tap).</summary>
        public bool CanUse { get; }

        public SignatureOptions(IReadOnlyList<SignatureMoveStats> moves, SignatureChargeState charge, bool canUse)
        {
            Moves = Guard.NotNull(moves, nameof(moves));
            Charge = charge;
            CanUse = canUse;
        }

        /// <summary>What the player's planning session allows this turn.</summary>
        public static SignatureOptions Of(PlanningSession session)
        {
            Guard.NotNull(session, nameof(session));
            return new SignatureOptions(session.Signatures, session.SignatureCharge, session.CanUseSignature);
        }
    }
}
