using System;
using XRim.Core;
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

        /// <summary>
        /// GDD §6 reach limit: arm length plus weapon length (<see cref="WeaponStats.LengthUnits"/>), measured from
        /// the shoulder. Arena units.
        /// </summary>
        [Placeholder("§6 arm length is not given")]
        public float ArmLengthUnits = 240f;

        /// <summary>Where the weapon arm's shoulder sits in the torso frame the path is drawn in (+X toward the opponent).</summary>
        [Placeholder("§6/§18 body proportions are not given")]
        public Vec2 ShoulderOffsetUnits = new Vec2(0f, 100f);
    }
}
