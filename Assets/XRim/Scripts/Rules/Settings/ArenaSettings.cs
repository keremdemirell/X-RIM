using System;
using XRim.Core.Gdd;

namespace XRim.Rules.Settings
{
    /// <summary>
    /// Arena geometry in abstract arena units (GDD §0, §18). How an arena unit maps to Unity world
    /// space is a physics-backend setting, not a rule.
    /// </summary>
    [Serializable]
    public sealed class ArenaSettings
    {
        [Placeholder("§13/§18 fixed arena width is TBD")]
        public float WidthUnits = 2000f;

        /// <summary>Distance between the dummies' starting lines at match start (positions then carry over, §3).</summary>
        [Placeholder("§3/§18 starting distance is not given")]
        public float StartingGapUnits = 700f;
    }
}
