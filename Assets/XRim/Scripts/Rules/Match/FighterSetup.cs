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

        public FighterSetup(Handedness handedness, IReadOnlyList<WeaponId> loadout)
        {
            Handedness = handedness;
            Loadout = Guard.NotNull(loadout, nameof(loadout));
        }
    }
}
