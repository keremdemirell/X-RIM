using System;
using XRim.Core;
using XRim.Rules.Settings;

namespace XRim.Rules.Combat
{
    /// <summary>
    /// Two-stage clash model (GDD §10, Decided; every threshold and weight Tunable). A pure decision: the simulation measures
    /// the facts, <see cref="WeaponContactResolver"/> applies the result to the turn.
    /// <list type="number">
    /// <item><b>Stage 1</b>, the contact angle: at or above <see cref="ClashSettings.HardClashAngleDegrees"/> is a hard clash,
    /// below it a glancing contact.</item>
    /// <item><b>Stage 2, hard clash:</b> power P = W_m·m + W_v·v. If the stronger power divided by the weaker reaches
    /// <see cref="ClashSettings.CrushRatio"/>, the stronger crushes through; otherwise both rebound.</item>
    /// <item><b>Stage 2, glancing:</b> the advantage flips to agility. Masses within
    /// <see cref="ClashSettings.GlancingSimilarMassBandFraction"/> of each other (the heavier at most that much heavier than the
    /// lighter, edge included) both slide past; otherwise the lighter weapon deflects the heavier.</item>
    /// </list>
    /// Shield contacts use <see cref="IShieldBlockModel"/> instead. A single formula with an angle multiplier is Rejected: the
    /// angle is the same for both weapons, so it cannot pick a winner.
    /// </summary>
    public sealed class ClashResolver
    {
        public ClashResult Resolve(ClashFacts facts, ClashSettings settings)
        {
            Guard.NotNull(settings, nameof(settings));
            if (facts.Left.Side != Side.Left || facts.Right.Side != Side.Right)
                throw new ArgumentException("The clash facts must give the left weapon first.", nameof(facts));

            float angle = facts.ContactAngleDegrees;
            float left = Power(facts.Left, settings);
            float right = Power(facts.Right, settings);
            if (angle >= settings.HardClashAngleDegrees)
            {
                Side stronger = left >= right ? Side.Left : Side.Right;
                return ReachesCrushRatio(Math.Max(left, right), Math.Min(left, right), settings.CrushRatio)
                    ? new ClashResult(ClashKind.CrushThrough, stronger, angle, left, right)
                    : new ClashResult(ClashKind.BothRebound, null, angle, left, right);
            }

            float heavier = Math.Max(facts.Left.Mass, facts.Right.Mass);
            float lighter = Math.Min(facts.Left.Mass, facts.Right.Mass);
            if (heavier <= lighter * (1f + settings.GlancingSimilarMassBandFraction))
                return new ClashResult(ClashKind.BothSlidePast, null, angle, left, right);

            Side lighterSide = facts.Left.Mass < facts.Right.Mass ? Side.Left : Side.Right;
            return new ClashResult(ClashKind.LighterDeflectsHeavier, lighterSide, angle, left, right);
        }

        /// <summary>P = W_m·m + W_v·v (§10 stage 2).</summary>
        public static float Power(ClashParticipant weapon, ClashSettings settings) =>
            Guard.NotNull(settings, nameof(settings)).MassWeight * weapon.Mass + settings.SpeedWeight * weapon.SpeedUnitsPerSecond;

        /// <summary>A weapon with no power at all is crushed by any power; two without power just rebound.</summary>
        private static bool ReachesCrushRatio(float stronger, float weaker, float crushRatio) =>
            weaker > 0f ? stronger / weaker >= crushRatio : stronger > 0f;
    }
}
