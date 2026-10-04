using System.Collections.Generic;
using XRim.Core;
using XRim.Rules.Settings;

namespace XRim.Rules.Paths
{
    /// <summary>Makes the live <see cref="PathPreview"/> of a stroke (GDD §6) with the same rules the authority applies to the plan.</summary>
    public sealed class PathPreviewBuilder
    {
        private readonly PathBuilder _builder;

        public PathPreviewBuilder(RulePolicies policies)
        {
            _builder = new PathBuilder(policies);
        }

        /// <param name="stroke">The raw points drawn so far, in the torso frame; none gives an empty preview with all the ink left.</param>
        /// <param name="weaponTipLocal">Where the weapon's tip rests now, in the torso frame (the lead-in starts there, D3).</param>
        /// <param name="weapon">The weapon with this turn's ink limit applied (<see cref="WeaponStats.WithInkLengthMultiplier"/>).</param>
        public PathPreview Build(IReadOnlyList<Vec2> stroke, Vec2 weaponTipLocal, WeaponStats weapon, PathSettings paths)
        {
            Guard.NotNull(weapon, nameof(weapon));
            Guard.NotNull(paths, nameof(paths));
            ReachLimit reach = ReachLimit.For(paths, weapon);
            if (stroke == null || stroke.Count == 0)
            {
                return new PathPreview(WeaponPath.Empty, WeaponPath.Empty, WeaponPath.Empty, weapon.InkThicknessUnits, weapon.InkLengthUnits, 0f,
                    reach, false, false, false);
            }

            var drawn = new WeaponPath(stroke);
            BuiltPath built = _builder.Build(WeaponPath.Empty, drawn, weaponTipLocal, weapon, paths);
            return new PathPreview(drawn, built.Path, built.InvalidTail, built.Inked.ThicknessUnits, built.Inked.BudgetUnits,
                built.Inked.Ink.CostUnits, reach, built.WasClampedByReach, built.Inked.WasCut, built.WasCutAtBreak);
        }
    }
}
