using System;
using System.Collections.Generic;
using XRim.Core;

namespace XRim.Tests.EditMode.Rules.Paths
{
    /// <summary>
    /// Builds raw touch input for a known shape the way a phone reports it: samples at a fixed touch rate while
    /// the finger speeds up and slows down, positions rounded to whole pixels at a given screen resolution,
    /// then converted back to arena units.
    /// </summary>
    internal static class FingerStroke
    {
        public static List<Vec2> Sample(Func<float, Vec2> shape, float durationSeconds, float touchRateHz,
            float pixelsPerArenaUnit)
        {
            int samples = (int)Math.Round(durationSeconds * touchRateHz);
            var points = new List<Vec2>(samples + 1);
            for (int i = 0; i <= samples; i++)
            {
                float time = (float)i / samples;
                float progress = time * time * (3f - 2f * time); // ease in and out: uneven finger speed
                Vec2 arena = shape(progress);
                var pixel = new Vec2((float)Math.Round(arena.X * pixelsPerArenaUnit), (float)Math.Round(arena.Y * pixelsPerArenaUnit));
                points.Add(pixel / pixelsPerArenaUnit);
            }

            return points;
        }

        /// <summary>A thrust that weaves up and down: 400 units forward, 60 units either side.</summary>
        public static Vec2 SCurve(float t) => new Vec2(400f * t, 60f * (float)Math.Sin(2.0 * Math.PI * t));

        /// <summary>A straight line from the origin along +X to <paramref name="lengthUnits"/>, then along
        /// <paramref name="turnDegrees"/> for another <paramref name="lengthUnits"/>.</summary>
        public static List<Vec2> Corner(float lengthUnits, float turnDegrees)
        {
            float radians = turnDegrees * XMath.DegreesToRadians;
            var corner = new Vec2(lengthUnits, 0f);
            var end = corner + new Vec2((float)Math.Cos(radians), (float)Math.Sin(radians)) * lengthUnits;
            return new List<Vec2> { Vec2.Zero, corner, end };
        }
    }
}
