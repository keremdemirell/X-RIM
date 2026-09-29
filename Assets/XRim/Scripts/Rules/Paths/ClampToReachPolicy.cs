using System;
using System.Collections.Generic;
using XRim.Core;

namespace XRim.Rules.Paths
{
    /// <summary>
    /// GDD §6, D4 (Decided 2026-09-29): clip the path at the reach limit by clamping. Points beyond reach are pulled
    /// onto the reach circle, so the weapon slides along the limit and the path continues when it comes back
    /// within reach (an overhead arc that pokes out at the top keeps its downswing). The body is never pulled.
    /// The clamped path is resampled again so its spacing stays even for the ink and rigidity rules.
    /// </summary>
    public sealed class ClampToReachPolicy : IReachPolicy
    {
        /// <summary>Points this close outside the circle count as on it (float error).</summary>
        private const float BoundaryToleranceUnits = 1e-3f;

        private readonly PathResampler _resampler = new PathResampler();

        public ReachResult Apply(WeaponPath sampledPath, ReachLimit limit, float sampleSpacingUnits)
        {
            Guard.NotNull(sampledPath, nameof(sampledPath));
            IReadOnlyList<Vec2> points = sampledPath.Points;
            bool anyOutside = false;
            for (int i = 0; i < points.Count && !anyOutside; i++)
            {
                anyOutside = !limit.Contains(points[i], BoundaryToleranceUnits);
            }

            if (!anyOutside)
            {
                return new ReachResult(sampledPath, false);
            }

            var clamped = new List<Vec2>(points.Count + 4);
            for (int i = 0; i < points.Count; i++)
            {
                if (i > 0)
                {
                    AddCircleCrossings(points[i - 1], points[i], limit, clamped);
                }

                clamped.Add(Clamp(points[i], limit));
            }

            return new ReachResult(_resampler.Resample(clamped, sampleSpacingUnits), true);
        }

        private static Vec2 Clamp(Vec2 point, ReachLimit limit)
        {
            Vec2 offset = point - limit.OriginUnits;
            float distance = offset.Length;
            return distance <= limit.RadiusUnits ? point : limit.OriginUnits + offset * (limit.RadiusUnits / distance);
        }

        /// <summary>Adds the points where segment a→b crosses the reach circle, in order along the segment.</summary>
        private static void AddCircleCrossings(Vec2 a, Vec2 b, ReachLimit limit, List<Vec2> output)
        {
            Vec2 direction = b - a;
            Vec2 fromOrigin = a - limit.OriginUnits;
            double qa = Vec2.Dot(direction, direction);
            if (qa <= 0.0)
            {
                return;
            }

            double qb = 2.0 * Vec2.Dot(fromOrigin, direction);
            double qc = Vec2.Dot(fromOrigin, fromOrigin) - (double)limit.RadiusUnits * limit.RadiusUnits;
            double discriminant = qb * qb - 4.0 * qa * qc;
            if (discriminant <= 0.0)
            {
                return;
            }

            double root = Math.Sqrt(discriminant);
            double first = (-qb - root) / (2.0 * qa);
            double second = (-qb + root) / (2.0 * qa);
            foreach (double t in new[] { first, second })
            {
                if (t > 0.0 && t < 1.0)
                {
                    output.Add(a + direction * (float)t);
                }
            }
        }
    }
}
