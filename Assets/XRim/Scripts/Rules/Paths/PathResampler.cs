using System;
using System.Collections.Generic;
using XRim.Core;

namespace XRim.Rules.Paths
{
    /// <summary>
    /// Turns raw touch points (already converted to arena units) into a point every N units
    /// (GDD §4, §18), so screen size, resolution and 60/120 Hz touch rates give the same path.
    /// Lives in Rules because the authority re-runs it when validating plans.
    /// </summary>
    public sealed class PathResampler
    {
        public WeaponPath Resample(IReadOnlyList<Vec2> rawArenaPoints, float sampleSpacingUnits)
        {
            // Placeholder: architecture setup only. Gameplay implementation comes later.
            throw new NotImplementedException("PathResampler.Resample is not implemented yet.");
        }
    }
}
