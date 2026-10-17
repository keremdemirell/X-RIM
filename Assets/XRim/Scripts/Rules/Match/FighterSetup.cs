using System.Collections.Generic;
using XRim.Core;

namespace XRim.Rules.Match
{
    /// <summary>What one player brings into a match. Cosmetics are deliberately absent: they never reach the rules.</summary>
    public sealed class FighterSetup
    {
        public Handedness Handedness { get; }

        /// <summary>The picked loadout slots (GDD §6), visible to both players at match start.</summary>
        public IReadOnlyList<WeaponId> Loadout { get; }

        /// <summary>
        /// The signature moves the player brings (GDD §8). Null: the whole catalogue, which is how every move is available in dev until
        /// the shop (Session 18) decides what a player owns (§16: earned currency only). Never shown to the opponent.
        /// </summary>
        public IReadOnlyList<SignatureMoveId> Signatures { get; }

        public FighterSetup(Handedness handedness, IReadOnlyList<WeaponId> loadout, IReadOnlyList<SignatureMoveId> signatures = null)
        {
            Handedness = handedness;
            Loadout = Guard.NotNull(loadout, nameof(loadout));
            Signatures = signatures;
        }
    }
}
