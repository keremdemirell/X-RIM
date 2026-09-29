using System;
using XRim.Core.Gdd;

namespace XRim.Rules.Settings
{
    /// <summary>
    /// Path capture rules that affect outcomes, so the authority re-applies them (GDD §4 input fairness, §18).
    /// Screen-only input tuning (body zone width, swipe window) lives in the Unity-side InputConfig.
    /// </summary>
    [Serializable]
    public sealed class PathSettings
    {
        /// <summary>GDD §4/§18: touch input is sampled into a point every N arena units.</summary>
        [Placeholder("§4 sample spacing N is Tunable with no starting value")]
        public float SampleSpacingUnits = 10f;
    }
}
