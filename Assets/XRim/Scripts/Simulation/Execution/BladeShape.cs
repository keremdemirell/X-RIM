using XRim.Core;
using XRim.Simulation.Physics;

namespace XRim.Simulation.Execution
{
    /// <summary>
    /// A held weapon as a thick line: from the grip (the body's position) to the tip along the body's rotation.
    /// Its width is the weapon's ink thickness, the hit width along the path (GDD §6, Decided).
    /// </summary>
    public readonly struct BladeShape
    {
        public float LengthUnits { get; }
        public float HalfWidthUnits { get; }

        public BladeShape(float lengthUnits, float widthUnits)
        {
            LengthUnits = lengthUnits;
            HalfWidthUnits = widthUnits * 0.5f;
        }

        /// <summary>Distance from a point to the blade's centre line at a pose.</summary>
        public float DistanceToCentreLine(Vec2 point, BodyPose pose)
        {
            Vec2 grip = pose.PositionUnits;
            Vec2 axis = Vec2.FromAngleDegrees(pose.RotationDegrees);
            float along = XMath.Clamp(Vec2.Dot(point - grip, axis), 0f, LengthUnits);
            return Vec2.Distance(point, grip + axis * along);
        }
    }
}
