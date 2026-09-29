using System;
using System.Collections.Generic;
using XRim.Core;

namespace XRim.Rules.Paths
{
    /// <summary>
    /// Turns raw touch points (already converted to arena units) into a point every N units along the stroke
    /// (GDD §4, §18), so screen size, resolution and 60/120 Hz touch rates give the same path.
    /// The output keeps positions only: finger speed never reaches the rules (§4 input fairness, Decided).
    /// Lives in Rules because the authority re-runs it when validating plans.
    /// </summary>
    public sealed class PathResampler
    {
        /// <summary>Raw points closer than this to the previous kept point are treated as the same point.</summary>
        private const double DuplicatePointUnits = 1e-4;

        /// <summary>
        /// Walks the raw polyline and places a point every <paramref name="sampleSpacingUnits"/> of arc length,
        /// starting at the first raw point. The last raw point is kept, so the final segment can be shorter.
        /// A stroke with a single distinct point returns a one-point path (a tap).
        /// </summary>
        public WeaponPath Resample(IReadOnlyList<Vec2> rawArenaPoints, float sampleSpacingUnits)
        {
            Guard.NotNull(rawArenaPoints, nameof(rawArenaPoints));
            double spacing = Guard.Positive(sampleSpacingUnits, nameof(sampleSpacingUnits));
            if (rawArenaPoints.Count == 0)
            {
                return WeaponPath.Empty;
            }

            var result = new List<Vec2> { rawArenaPoints[0] };
            double previousX = rawArenaPoints[0].X;
            double previousY = rawArenaPoints[0].Y;
            double distanceToNextSample = spacing;
            for (int i = 1; i < rawArenaPoints.Count; i++)
            {
                double targetX = rawArenaPoints[i].X;
                double targetY = rawArenaPoints[i].Y;
                double segmentX = targetX - previousX;
                double segmentY = targetY - previousY;
                double segmentLength = Math.Sqrt(segmentX * segmentX + segmentY * segmentY);
                if (segmentLength <= DuplicatePointUnits)
                {
                    continue;
                }

                double travelled = 0.0;
                while (segmentLength - travelled >= distanceToNextSample)
                {
                    travelled += distanceToNextSample;
                    double t = travelled / segmentLength;
                    result.Add(new Vec2((float)(previousX + segmentX * t), (float)(previousY + segmentY * t)));
                    distanceToNextSample = spacing;
                }

                distanceToNextSample -= segmentLength - travelled;
                previousX = targetX;
                previousY = targetY;
            }

            double sinceLastSample = spacing - distanceToNextSample;
            if (sinceLastSample > DuplicatePointUnits)
            {
                result.Add(new Vec2((float)previousX, (float)previousY));
            }

            return new WeaponPath(result);
        }
    }
}
