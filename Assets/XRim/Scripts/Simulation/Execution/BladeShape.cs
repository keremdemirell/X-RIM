using XRim.Core;
using XRim.Rules.Settings;
using XRim.Simulation.Physics;

namespace XRim.Simulation.Execution
{
    /// <summary>
    /// A held item as a thick line: its box's long centre line, carried by the body's pose, and half its thickness. A weapon's
    /// line runs from the grip to the tip and its width is the weapon's ink thickness, the hit width along the path (GDD §6,
    /// Decided); the shield's runs across its face (<see cref="HeldItemShape"/>).
    /// </summary>
    public readonly struct BladeShape
    {
        private readonly Vec2 _startLocal;
        private readonly Vec2 _endLocal;

        public float HalfWidthUnits { get; }

        /// <summary>A blade from the grip to its tip along the body's rotation.</summary>
        public BladeShape(float lengthUnits, float widthUnits) : this(Vec2.Zero, new Vec2(lengthUnits, 0f), widthUnits * 0.5f)
        {
        }

        private BladeShape(Vec2 startLocal, Vec2 endLocal, float halfWidthUnits)
        {
            _startLocal = startLocal;
            _endLocal = endLocal;
            HalfWidthUnits = halfWidthUnits;
        }

        public static BladeShape Of(WeaponStats item)
        {
            HeldItemShape shape = HeldItemShape.Of(item);
            return new BladeShape(shape.CentreLineStartLocal, shape.CentreLineEndLocal, shape.HalfThicknessUnits);
        }

        public float LengthUnits => Vec2.Distance(_startLocal, _endLocal);

        /// <summary>Distance from a point to the centre line at a pose.</summary>
        public float DistanceToCentreLine(Vec2 point, BodyPose pose)
        {
            Vec2 start = HeldItemShape.ToArena(_startLocal, pose);
            Vec2 line = HeldItemShape.ToArena(_endLocal, pose) - start;
            float lengthSquared = line.LengthSquared;
            float along = lengthSquared > 0f ? XMath.Clamp01(Vec2.Dot(point - start, line) / lengthSquared) : 0f;
            return Vec2.Distance(point, start + line * along);
        }
    }
}
