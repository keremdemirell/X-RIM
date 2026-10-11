using System;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Limbs;
using XRim.Rules.Settings;
using XRim.Simulation.Settings;

namespace XRim.Simulation.Physics
{
    /// <summary>
    /// The shape of a severed arm or leg on the floor, from the placeholder ragdoll's layout (the same lengths the builder uses), and
    /// how it turns into a club and back (GDD §12). A wielded club is gripped at the end that was jointed and lies along the club's
    /// axis, so a club dropped back to the floor lies straight where the club was.
    /// </summary>
    public static class SeveredLimbLayout
    {
        /// <summary>A limb's axis points down (−90°) at rotation 0, so its rotation is its direction plus this.</summary>
        private const float LimbAxisOffsetDegrees = 90f;

        private static readonly Vec2 Down = new Vec2(0f, -1f);

        public static float LengthUnits(BodyPart part, RagdollSettings body, PathSettings paths)
        {
            Guard.NotNull(body, nameof(body));
            Guard.NotNull(paths, nameof(paths));
            if (part.IsArm()) return paths.ArmLengthUnits;
            if (part.IsLeg()) return body.LegLengthUnits;
            throw new ArgumentException($"{part} cannot be severed (GDD §11).", nameof(part));
        }

        /// <summary>The length of the limb's upper segment with ten bodies (where the elbow or knee is).</summary>
        public static float UpperLengthUnits(BodyPart part, RagdollSettings body, PathSettings paths) =>
            LengthUnits(part, body, paths) * body.UpperSegmentFraction;

        /// <summary>A club let go of: the limb lies where the club was, pivot at the grip, straight along the club.</summary>
        public static SeveredLimbPose FromClub(SeveredLimbRef limb, BodyPose club, RagdollSegmentation segmentation, RagdollSettings body,
            PathSettings paths)
        {
            var pose = new BodyPose(club.PositionUnits, club.RotationDegrees + LimbAxisOffsetDegrees);
            if (segmentation != RagdollSegmentation.TenBodies) return new SeveredLimbPose(limb.Owner, limb.Part, pose);

            Vec2 elbow = club.PositionUnits + Vec2.FromAngleDegrees(club.RotationDegrees) * UpperLengthUnits(limb.Part, body, paths);
            return new SeveredLimbPose(limb.Owner, limb.Part, pose, new BodyPose(elbow, pose.RotationDegrees));
        }

        /// <summary>How far a point is from the nearest point of a limb lying on the floor (for retrieving it, GDD §12).</summary>
        public static float DistanceTo(SeveredLimbPose limb, Vec2 point, RagdollSettings body, PathSettings paths)
        {
            float length = LengthUnits(limb.Part, body, paths);
            if (!limb.HasLower) return DistanceToSegment(point, limb.Pose.PositionUnits, EndOf(limb.Pose, length));

            float upper = UpperLengthUnits(limb.Part, body, paths);
            float toUpper = DistanceToSegment(point, limb.Pose.PositionUnits, EndOf(limb.Pose, upper));
            float toLower = DistanceToSegment(point, limb.Lower.PositionUnits, EndOf(limb.Lower, length - upper));
            return Math.Min(toUpper, toLower);
        }

        private static Vec2 EndOf(BodyPose segment, float lengthUnits) => segment.PositionUnits + Down.Rotated(segment.RotationDegrees) * lengthUnits;

        private static float DistanceToSegment(Vec2 point, Vec2 start, Vec2 end)
        {
            Vec2 along = end - start;
            float lengthSquared = Vec2.Dot(along, along);
            float t = lengthSquared > 0f ? XMath.Clamp01(Vec2.Dot(point - start, along) / lengthSquared) : 0f;
            return Vec2.Distance(point, start + along * t);
        }
    }
}
