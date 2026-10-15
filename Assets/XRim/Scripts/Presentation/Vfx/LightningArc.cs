using System;
using XRim.Core;

namespace XRim.Presentation.Vfx
{
    /// <summary>
    /// A jagged electric arc between two points (GDD §13): the straight line broken into segments, each inner point pushed sideways by
    /// a seeded random amount, most in the middle and none at the ends, so it always joins its two points. The same seed draws the same
    /// arc; a new seed every few hundredths of a second makes it crackle.
    /// </summary>
    public static class LightningArc
    {
        public static void Fill(Vec2 from, Vec2 to, float jitterUnits, uint seed, Vec2[] points)
        {
            Guard.NotNull(points, nameof(points));
            int count = points.Length;
            if (count == 0) return;
            points[0] = from;
            if (count == 1) return;

            Vec2 along = to - from;
            float length = along.Length;
            Vec2 side = length > 0f ? new Vec2(-along.Y / length, along.X / length) : Vec2.UnitX;
            var random = new XorShiftRandom(seed == 0u ? 1u : seed);
            for (int i = 1; i < count - 1; i++)
            {
                float t = (float)i / (count - 1);
                float taper = (float)Math.Sin(Math.PI * t);
                float offset = (random.NextFloat() * 2f - 1f) * jitterUnits * taper;
                points[i] = Vec2.Lerp(from, to, t) + side * offset;
            }

            points[count - 1] = to;
        }
    }
}
