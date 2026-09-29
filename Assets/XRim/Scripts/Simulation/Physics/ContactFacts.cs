using XRim.Core;

namespace XRim.Simulation.Physics
{
    /// <summary>
    /// A contact that began during a physics step, in engine-free terms. Normal and relative velocity feed the
    /// clash angle (GDD §10, §18); the execution loop adds path distance to get time-to-impact.
    /// </summary>
    public readonly struct ContactFacts
    {
        public BodyTag A { get; }
        public BodyTag B { get; }
        public Vec2 PointUnits { get; }

        /// <summary>Unit normal pointing from A to B.</summary>
        public Vec2 Normal { get; }

        /// <summary>Velocity of B relative to A, arena units per second.</summary>
        public Vec2 RelativeVelocityUnitsPerSecond { get; }

        public ContactFacts(BodyTag a, BodyTag b, Vec2 pointUnits, Vec2 normal, Vec2 relativeVelocityUnitsPerSecond)
        {
            A = a;
            B = b;
            PointUnits = pointUnits;
            Normal = normal;
            RelativeVelocityUnitsPerSecond = relativeVelocityUnitsPerSecond;
        }
    }
}
