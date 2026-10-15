using XRim.Core;
using XRim.Rules.Settings;

namespace XRim.Rules.Paths
{
    /// <summary>
    /// Debug and tests (the infinite-ink cheat, Session 13): measures a path as the model it wraps does (its length, and where a too-sharp
    /// turn breaks it) but charges no ink, so no path is cut short or refused for its ink. Reach still clamps it (D4) and the execution
    /// hard cap still ends it. Swapped into a match's <see cref="RulePolicies.InkCost"/> by the debug tools only: never a game rule.
    /// </summary>
    public sealed class FreeInkCostModel : IInkCostModel
    {
        public FreeInkCostModel(IInkCostModel inner)
        {
            Inner = Guard.NotNull(inner, nameof(inner));
        }

        /// <summary>The model it wraps, which the cheat puts back when it is switched off.</summary>
        public IInkCostModel Inner { get; }

        public InkMeasurement Measure(WeaponPath path, WeaponStats weapon)
        {
            InkMeasurement measured = Inner.Measure(path, weapon);
            return new InkMeasurement(measured.LengthUnits, new float[measured.CumulativeCostUnits.Count], measured.FirstInvalidPointIndex);
        }
    }
}
