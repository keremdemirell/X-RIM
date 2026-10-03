using XRim.Core;
using XRim.Rules;
using XRim.Simulation.Settings;

namespace XRim.Simulation.Physics
{
    /// <summary>
    /// Where a dummy's feet and floor are, from the placeholder ragdoll's layout (legs hang from the pelvis, the torso's
    /// pivot, and a limb lies along its local −Y at rotation 0).
    /// </summary>
    public static class LegGeometry
    {
        /// <summary>The top of the floor. The physics world builds its floor here.</summary>
        public const float FloorYUnits = 0f;

        private static readonly Vec2 Down = new Vec2(0f, -1f);

        /// <summary>The pelvis height of a dummy standing straight with its soles on the floor.</summary>
        public static float StandingPelvisHeightUnits(RagdollSettings body) => FloorYUnits + Guard.NotNull(body, nameof(body)).LegLengthUnits;

        /// <summary>The leg that leads a stride: the dominant side's, like a fencer's front foot.</summary>
        public static BodyPart FrontLeg(Handedness handedness) => handedness == Handedness.Right ? BodyPart.RightLeg : BodyPart.LeftLeg;

        public static BodyPart BackLeg(Handedness handedness) => handedness == Handedness.Right ? BodyPart.LeftLeg : BodyPart.RightLeg;

        /// <summary>The thigh's length with ten bodies (where the knee is).</summary>
        public static float ThighLengthUnits(RagdollSettings body) => body.LegLengthUnits * body.UpperSegmentFraction;

        /// <summary>Where a leg's sole is in a pose: the far end of its last segment.</summary>
        public static Vec2 SoleOf(FighterPose pose, BodyPart leg, RagdollSettings body)
        {
            Guard.NotNull(pose, nameof(pose));
            Guard.NotNull(body, nameof(body));
            if (!pose.HasLowerSegments) return EndOf(pose.Get(leg), body.LegLengthUnits);
            return EndOf(pose.GetLower(leg), body.LegLengthUnits - ThighLengthUnits(body));
        }

        private static Vec2 EndOf(BodyPose segment, float lengthUnits) =>
            segment.PositionUnits + Down.Rotated(segment.RotationDegrees) * lengthUnits;
    }
}
