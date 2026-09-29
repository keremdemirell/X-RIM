using System;
using XRim.Core;
using XRim.Rules.Settings;

namespace XRim.Rules.Paths
{
    /// <summary>
    /// GDD §6 spear rigidity (TBD, conditional: the whole rule stays behind <see cref="RigiditySettings.Enabled"/>).
    /// For a weapon with rigidity enabled:
    /// <list type="bullet">
    /// <item>Each segment whose start turns by Δθ above <see cref="RigiditySettings.BendThresholdDegrees"/> costs
    /// distance × (1 + k·Δθ), with Δθ the whole turn in degrees, as the formula is written (designer, 2026-09-29).</item>
    /// <item>A very sharp turn breaks the path. The turn is measured as the direction change within
    /// <see cref="RigiditySettings.BreakWindowUnits"/> of arc length, so a corner that falls between two samples
    /// (and is split into two smaller turns) still counts as one turn.</item>
    /// </list>
    /// Weapons without rigidity pay plain length, exactly like <see cref="LengthInkCostModel"/>.
    /// </summary>
    public sealed class RigidityInkCostModel : IInkCostModel
    {
        /// <summary>Absorbs float error when an arc length lands exactly on the window's end.</summary>
        private const float WindowToleranceUnits = 1e-3f;

        private readonly LengthInkCostModel _plain = new LengthInkCostModel();

        public InkMeasurement Measure(WeaponPath path, WeaponStats weapon)
        {
            Guard.NotNull(path, nameof(path));
            Guard.NotNull(weapon, nameof(weapon));
            RigiditySettings rigidity = weapon.Rigidity;
            if (rigidity == null || !rigidity.Enabled)
            {
                return _plain.Measure(path, weapon);
            }

            int count = path.Points.Count;
            var cumulative = new float[count];
            if (count < 2)
            {
                return new InkMeasurement(0f, cumulative, InkMeasurement.NoInvalidPoint);
            }

            // turns[i]: signed turn in degrees at point i, between segment i-1 and segment i (0 at both ends).
            var turns = new float[count];
            var arc = new float[count];
            for (int i = 1; i < count; i++)
            {
                arc[i] = arc[i - 1] + Vec2.Distance(path.Points[i - 1], path.Points[i]);
                if (i < count - 1)
                {
                    turns[i] = SignedTurnDegrees(path.Points[i - 1], path.Points[i], path.Points[i + 1]);
                }
            }

            float total = 0f;
            for (int i = 0; i < count - 1; i++)
            {
                float distance = arc[i + 1] - arc[i];
                float bend = Math.Abs(turns[i]);
                float multiplier = bend > rigidity.BendThresholdDegrees ? 1f + rigidity.BendCostK * bend : 1f;
                total += distance * multiplier;
                cumulative[i + 1] = total;
            }

            return new InkMeasurement(arc[count - 1], cumulative, FirstBreak(turns, arc, rigidity));
        }

        /// <summary>
        /// The first point at which the direction has changed by the break angle or more within the window,
        /// that is, where a sharp turn completes.
        /// </summary>
        private static int FirstBreak(float[] turns, float[] arc, RigiditySettings rigidity)
        {
            int first = InkMeasurement.NoInvalidPoint;
            for (int start = 1; start < turns.Length - 1; start++)
            {
                float accumulated = 0f;
                for (int end = start; end < turns.Length - 1; end++)
                {
                    if (arc[end] - arc[start] > rigidity.BreakWindowUnits + WindowToleranceUnits)
                    {
                        break;
                    }

                    accumulated += turns[end];
                    if (Math.Abs(accumulated) >= rigidity.BreakAngleDegrees)
                    {
                        if (first == InkMeasurement.NoInvalidPoint || end < first)
                        {
                            first = end;
                        }

                        break;
                    }
                }
            }

            return first;
        }

        private static float SignedTurnDegrees(Vec2 previous, Vec2 current, Vec2 next)
        {
            Vec2 incoming = current - previous;
            Vec2 outgoing = next - current;
            double radians = Math.Atan2(Vec2.Cross(incoming, outgoing), Vec2.Dot(incoming, outgoing));
            return (float)radians * XMath.RadiansToDegrees;
        }
    }
}
