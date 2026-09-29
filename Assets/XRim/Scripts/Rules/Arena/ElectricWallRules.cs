using System;
using XRim.Core;
using XRim.Rules.Settings;

namespace XRim.Rules.Arena
{
    /// <summary>
    /// Electric wall sequence (GDD §13, Decided). Only the player's input counts: a backward swipe triggers it;
    /// being knocked back, holding a shield or standing still never does.
    /// Turn N backward: warning wall, no damage. N+1 anything else: wall disappears.
    /// N+1 backward again: damage + bounce, wall advances. N+2 and later backward: again.
    /// </summary>
    public sealed class ElectricWallRules
    {
        public void OnPlansLocked(ElectricWallState wall, Side side, BodyMove plannedMove, float fighterXUnits,
            ElectricWallSettings settings)
        {
            // Placeholder: architecture setup only. Gameplay implementation comes later.
            throw new NotImplementedException("ElectricWallRules.OnPlansLocked is not implemented yet.");
        }
    }
}
