using XRim.Core;
using XRim.Simulation.Physics;

namespace XRim.Simulation.Drivers
{
    /// <summary>
    /// The frame paths are drawn in (GDD §6, Decided: the path moves with the body). Its origin is the torso's pivot
    /// (the pelvis) and +X points toward the opponent, so the right-hand fighter's frame is mirrored. The frame follows
    /// the torso's position and rotation.
    /// </summary>
    public static class TorsoFrame
    {
        private const float MirroredHalfTurnDegrees = 180f;

        public static Vec2 ToArena(Vec2 local, BodyPose torso, Side side) =>
            torso.PositionUnits + Mirror(local, side).Rotated(torso.RotationDegrees);

        public static Vec2 ToLocal(Vec2 arena, BodyPose torso, Side side) =>
            Mirror((arena - torso.PositionUnits).Rotated(-torso.RotationDegrees), side);

        /// <summary>A direction angle in the frame, as an arena angle.</summary>
        public static float AngleToArena(float localDegrees, BodyPose torso, Side side) =>
            MirrorAngle(localDegrees, side) + torso.RotationDegrees;

        public static float AngleToLocal(float arenaDegrees, BodyPose torso, Side side) =>
            MirrorAngle(arenaDegrees - torso.RotationDegrees, side);

        /// <summary>
        /// A body pose in the frame, as an arena pose. Only valid for bodies that look the same when mirrored along
        /// their long axis (every placeholder part and weapon is a box or a circle).
        /// </summary>
        public static BodyPose ToArena(BodyPose local, BodyPose torso, Side side) =>
            new BodyPose(ToArena(local.PositionUnits, torso, side), AngleToArena(local.RotationDegrees, torso, side));

        private static Vec2 Mirror(Vec2 vector, Side side) => side == Side.Right ? new Vec2(-vector.X, vector.Y) : vector;

        private static float MirrorAngle(float degrees, Side side) => side == Side.Right ? MirroredHalfTurnDegrees - degrees : degrees;
    }
}
