using System.Collections.Generic;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Signature;

namespace XRim.Networking.Protocol
{
    /// <summary>
    /// What one player may do with signature moves this turn (GDD §8), as the server sends it to that player only: the moves brought (by id;
    /// the phone finds their patterns in the settings' catalogue), the charge and whether a move can be used. The opponent never receives it.
    /// </summary>
    public sealed class SignatureOffer
    {
        public static SignatureOffer None { get; } = new SignatureOffer(new SignatureMoveId[0], default, false);

        public IReadOnlyList<SignatureMoveId> Moves { get; }

        public SignatureChargeState Charge { get; }

        public bool CanUse { get; }

        public SignatureOffer(IReadOnlyList<SignatureMoveId> moves, SignatureChargeState charge, bool canUse)
        {
            Moves = Guard.NotNull(moves, nameof(moves));
            Charge = charge;
            CanUse = canUse;
        }

        /// <summary>The offer a planning session's options make (the server's side of a <see cref="PlanningWindow"/>).</summary>
        public static SignatureOffer Of(SignatureOptions options)
        {
            Guard.NotNull(options, nameof(options));
            var moves = new SignatureMoveId[options.Moves.Count];
            for (int i = 0; i < moves.Length; i++)
            {
                moves[i] = options.Moves[i].MoveId;
            }

            return new SignatureOffer(moves, options.Charge, options.CanUse);
        }
    }
}
