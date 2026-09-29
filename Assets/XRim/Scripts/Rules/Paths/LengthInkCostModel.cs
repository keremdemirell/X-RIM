using XRim.Core;
using XRim.Rules.Settings;

namespace XRim.Rules.Paths
{
    /// <summary>GDD §6 ink budget: a path costs its length, whatever its shape. Never invalid.</summary>
    public sealed class LengthInkCostModel : IInkCostModel
    {
        public InkMeasurement Measure(WeaponPath path, WeaponStats weapon)
        {
            Guard.NotNull(path, nameof(path));
            var cumulative = new float[path.Points.Count];
            float total = 0f;
            for (int i = 1; i < cumulative.Length; i++)
            {
                total += Vec2.Distance(path.Points[i - 1], path.Points[i]);
                cumulative[i] = total;
            }

            return new InkMeasurement(total, cumulative, InkMeasurement.NoInvalidPoint);
        }
    }
}
