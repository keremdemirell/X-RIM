using XRim.Core;
using XRim.Rules;
using XRim.Simulation.Physics;

namespace XRim.Input
{
    /// <summary>
    /// How far the body zone must reach to cover the player's own dummy (GDD §4, Decided: "about 15% of the screen width, covering the
    /// player's own dummy"; designer 2026-10-11, F3: the zone runs from the player's screen edge to just past their dummy). Measured from
    /// the frozen board's pose and what the camera shows, in arena units, so neither the screen's size nor a view flip changes it: the
    /// player's own dummy is always drawn at their own edge, so the distance from that edge to the dummy's front is the same either way.
    /// The front is the farthest of the dummy's head, torso and legs toward the opponent; the arms are left out, so a raised weapon never
    /// widens the zone. Pure arithmetic, so it is tested without a screen.
    /// </summary>
    public static class BodyZoneCover
    {
        private static readonly BodyPart[] CoveredParts = { BodyPart.Head, BodyPart.Torso, BodyPart.LeftLeg, BodyPart.RightLeg };

        /// <summary>
        /// The share of the screen width, measured from the player's own screen edge, that reaches <paramref name="marginUnits"/> past
        /// the front of their dummy (0..1).
        /// </summary>
        /// <param name="viewCentreXUnits">The arena x at the centre of the screen, in the canonical (unflipped) arena.</param>
        /// <param name="viewWidthUnits">How many arena units the screen's width shows.</param>
        public static float FromOwnEdgeFraction(FighterPose pose, Side side, float viewCentreXUnits, float viewWidthUnits, float marginUnits)
        {
            Guard.NotNull(pose, nameof(pose));
            Guard.Positive(viewWidthUnits, nameof(viewWidthUnits));

            // Distances along the fighter's forward direction (+X for the left fighter, -X for the right one).
            float facing = side.FacingSign();
            float front = float.NegativeInfinity;
            foreach (BodyPart part in CoveredParts)
            {
                if (part.IsSeverable() && pose.IsSevered(part)) continue;
                front = System.Math.Max(front, facing * pose.Get(part).PositionUnits.X);
                if (pose.HasLowerSegments && part.IsLeg()) front = System.Math.Max(front, facing * pose.GetLower(part).PositionUnits.X);
            }

            float edgeBehind = facing * viewCentreXUnits - viewWidthUnits * 0.5f;
            return XMath.Clamp01((front + marginUnits - edgeBehind) / viewWidthUnits);
        }
    }
}
