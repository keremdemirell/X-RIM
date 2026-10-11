using System;
using XRim.Core.Gdd;

namespace XRim.Simulation.Settings
{
    /// <summary>
    /// How a severed limb leaves the body (GDD §12: "the limb falls under gravity"; Session 11). The joint breaks by logic, then the limb
    /// pops off backward (away from the opponent) and up, spinning backward, so it always falls over: a leg cut while standing would
    /// otherwise stay balanced on its sole, and a hanging arm could land on its end. Feel only: it changes neither damage nor rules.
    /// Session 13 polishes it.
    /// </summary>
    [Serializable]
    public sealed class SeverReactionSettings
    {
        [Placeholder("§12: how hard a severed limb pops off has no value")]
        public float PopBackSpeedUnitsPerSecond = 120f;

        [Placeholder("§12: how hard a severed limb pops off has no value")]
        public float PopUpSpeedUnitsPerSecond = 80f;

        [Placeholder("§12: how fast a severed limb spins as it pops off has no value")]
        public float PopSpinDegreesPerSecond = 270f;
    }
}
