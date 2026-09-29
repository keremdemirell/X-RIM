using System.Collections.Generic;
using XRim.Core;
using XRim.Rules.Settings;

namespace XRim.Rules.Paths
{
    /// <summary>
    /// Turns a drawn stroke into the path that executes, applying the GDD §6 path rules in one fixed order, so the
    /// drawing preview (Session 08) and the authority's validation (Session 03) always agree:
    /// <list type="number">
    /// <item>stroke policy: combine with the stroke already drawn (D5: the new stroke replaces it);</item>
    /// <item>start policy: add the lead-in from the weapon tip (D3), so the lead-in costs ink and time;</item>
    /// <item>resample every <see cref="PathSettings.SampleSpacingUnits"/> (§4);</item>
    /// <item>reach policy: clamp to arm length plus weapon length (D4), so clamped-away parts cost no ink;</item>
    /// <item>ink cut-off: stop where the ink runs out (§6, Decided);</item>
    /// <item>a too-sharp turn (rigid weapons only) cuts the path where the turn completes (designer, 2026-09-29).</item>
    /// </list>
    /// Paths are in the torso frame, arena units, +X toward the opponent (§6, Decided: the path moves with the body).
    /// </summary>
    public sealed class PathBuilder
    {
        private readonly RulePolicies _policies;
        private readonly PathResampler _resampler = new PathResampler();
        private readonly InkCutoff _cutoff = new InkCutoff();

        public PathBuilder(RulePolicies policies)
        {
            _policies = Guard.NotNull(policies, nameof(policies));
        }

        /// <param name="drawnSoFar">The <see cref="BuiltPath.Drawn"/> of the previous stroke this turn, or <see cref="WeaponPath.Empty"/>.</param>
        /// <param name="newStroke">The new stroke's raw points, converted to torso-frame arena units.</param>
        /// <param name="weaponTipLocal">Where the weapon's tip is now, in the torso frame.</param>
        public BuiltPath Build(WeaponPath drawnSoFar, WeaponPath newStroke, Vec2 weaponTipLocal, WeaponStats weapon,
            PathSettings settings)
        {
            Guard.NotNull(weapon, nameof(weapon));
            Guard.NotNull(settings, nameof(settings));
            WeaponPath drawn = _policies.Stroke.Combine(drawnSoFar ?? WeaponPath.Empty, Guard.NotNull(newStroke, nameof(newStroke)));
            WeaponPath started = _policies.PathStart.ResolveStart(drawn, weaponTipLocal);
            WeaponPath sampled = _resampler.Resample(started.Points, settings.SampleSpacingUnits);
            ReachResult reach = _policies.Reach.Apply(sampled, ReachLimit.For(settings, weapon), settings.SampleSpacingUnits);
            InkedPath inked = _cutoff.Apply(reach.Path, weapon, _policies.InkCost);

            bool cutAtBreak = !inked.Ink.IsValid;
            if (cutAtBreak)
            {
                WeaponPath kept = KeepUpTo(inked.Path, inked.Ink.FirstInvalidPointIndex);
                inked = new InkedPath(kept, _policies.InkCost.Measure(kept, weapon), inked.BudgetUnits,
                    inked.ThicknessUnits, inked.WasCut);
            }

            return new BuiltPath(drawn, reach.Path, inked, reach.WasClamped, cutAtBreak);
        }

        private static WeaponPath KeepUpTo(WeaponPath path, int lastIndex)
        {
            var kept = new List<Vec2>(lastIndex + 1);
            for (int i = 0; i <= lastIndex && i < path.Points.Count; i++)
            {
                kept.Add(path.Points[i]);
            }

            return new WeaponPath(kept);
        }
    }
}
