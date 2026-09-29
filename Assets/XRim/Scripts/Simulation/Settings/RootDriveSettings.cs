using System;
using XRim.Core.Gdd;

namespace XRim.Simulation.Settings
{
    /// <summary>
    /// How the torso holds its root target: standing still in place, or following a body move (GDD §5). The strengths
    /// are accelerations, so they feel the same when the torso's mass changes.
    /// </summary>
    [Serializable]
    public sealed class RootDriveSettings
    {
        public RootDriveMode Mode = RootDriveMode.Powered;

        /// <summary>How hard the torso is pulled back to its root. Lower = hits knock the dummy around more.</summary>
        [Placeholder("Session 02 spike: standing strength, tuned at PT1")]
        public float MaxAccelerationUnitsPerSecondSquared = 20000f;

        /// <summary>How hard the torso is turned upright. Lower = hits make the dummy reel more.</summary>
        [Placeholder("Session 02 spike: upright strength, tuned at PT1")]
        public float MaxAngularAccelerationDegreesPerSecondSquared = 20000f;

        /// <summary>Share of the remaining error corrected each step (0..1). Lower = softer, springier recovery.</summary>
        [Placeholder("Session 02 spike: standing recovery softness, tuned at PT1")]
        public float CorrectionFraction = 0.3f;
    }
}
