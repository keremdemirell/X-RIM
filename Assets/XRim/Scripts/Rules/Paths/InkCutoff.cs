using System.Collections.Generic;
using XRim.Core;
using XRim.Rules.Settings;

namespace XRim.Rules.Paths
{
    /// <summary>
    /// GDD §6 (Decided): drawing stops when the ink runs out. The path is cut at the exact point where the
    /// ink spent, as the cost model counts it, reaches the weapon's ink length. With a bend penalty the path
    /// therefore ends sooner than its plain length would allow.
    /// </summary>
    public sealed class InkCutoff
    {
        /// <summary>Absorbs float error so a path that uses exactly its whole budget is not cut.</summary>
        private const float BudgetToleranceUnits = 1e-3f;

        public InkedPath Apply(WeaponPath path, WeaponStats weapon, IInkCostModel costModel)
        {
            Guard.NotNull(path, nameof(path));
            Guard.NotNull(weapon, nameof(weapon));
            Guard.NotNull(costModel, nameof(costModel));

            float budget = weapon.InkLengthUnits;
            InkMeasurement ink = costModel.Measure(path, weapon);
            if (ink.CostUnits <= budget + BudgetToleranceUnits)
            {
                return new InkedPath(path, ink, budget, weapon.InkThicknessUnits, false);
            }

            WeaponPath cut = Cut(path, ink, budget);
            return new InkedPath(cut, costModel.Measure(cut, weapon), budget, weapon.InkThicknessUnits, true);
        }

        private static WeaponPath Cut(WeaponPath path, InkMeasurement ink, float budget)
        {
            IReadOnlyList<float> cumulative = ink.CumulativeCostUnits;
            var kept = new List<Vec2> { path.Points[0] };
            for (int i = 1; i < path.Points.Count; i++)
            {
                if (cumulative[i] <= budget)
                {
                    kept.Add(path.Points[i]);
                    continue;
                }

                float segmentCost = cumulative[i] - cumulative[i - 1];
                float fraction = segmentCost > 0f ? (budget - cumulative[i - 1]) / segmentCost : 0f;
                if (fraction > 0f)
                {
                    kept.Add(Vec2.Lerp(path.Points[i - 1], path.Points[i], fraction));
                }

                break;
            }

            return new WeaponPath(kept);
        }
    }
}
