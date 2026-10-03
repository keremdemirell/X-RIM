using XRim.Core;
using XRim.Simulation.Physics;

namespace XRim.Simulation.Drivers
{
    /// <summary>
    /// What a weapon driver asks the physics world to do with the held item for one step. Accelerations are mass-free:
    /// the world multiplies them by the body's mass and inertia, so gains feel the same for a rapier and a mace.
    /// </summary>
    public readonly struct HeldItemCommand
    {
        public HeldItemCommandKind Kind { get; }

        /// <summary>For <see cref="HeldItemCommandKind.MoveTo"/>: the pose to reach by the end of the step, arena units.</summary>
        public BodyPose Target { get; }

        /// <summary>For <see cref="HeldItemCommandKind.Push"/>.</summary>
        public Vec2 AccelerationUnitsPerSecondSquared { get; }

        /// <summary>For <see cref="HeldItemCommandKind.Push"/>. Counter-clockwise positive.</summary>
        public float AngularAccelerationDegreesPerSecondSquared { get; }

        /// <summary>For <see cref="HeldItemCommandKind.Release"/>: the free body's starting velocity.</summary>
        public Vec2 VelocityUnitsPerSecond { get; }

        /// <summary>For <see cref="HeldItemCommandKind.Release"/>. Counter-clockwise positive.</summary>
        public float AngularVelocityDegreesPerSecond { get; }

        private HeldItemCommand(HeldItemCommandKind kind, BodyPose target, Vec2 acceleration, float angularAcceleration, Vec2 velocity,
            float angularVelocity)
        {
            Kind = kind;
            Target = target;
            AccelerationUnitsPerSecondSquared = acceleration;
            AngularAccelerationDegreesPerSecondSquared = angularAcceleration;
            VelocityUnitsPerSecond = velocity;
            AngularVelocityDegreesPerSecond = angularVelocity;
        }

        public static HeldItemCommand MoveTo(BodyPose target) =>
            new HeldItemCommand(HeldItemCommandKind.MoveTo, target, Vec2.Zero, 0f, Vec2.Zero, 0f);

        public static HeldItemCommand Push(Vec2 accelerationUnitsPerSecondSquared, float angularAccelerationDegreesPerSecondSquared) =>
            new HeldItemCommand(HeldItemCommandKind.Push, default, accelerationUnitsPerSecondSquared, angularAccelerationDegreesPerSecondSquared,
                Vec2.Zero, 0f);

        /// <summary>The held item becomes a free body moving at this velocity (a stopped kinematic blade handed to physics, D1).</summary>
        public static HeldItemCommand Release(Vec2 velocityUnitsPerSecond, float angularVelocityDegreesPerSecond) =>
            new HeldItemCommand(HeldItemCommandKind.Release, default, Vec2.Zero, 0f, velocityUnitsPerSecond, angularVelocityDegreesPerSecond);

        /// <summary>No drive at all: the held item moves under physics only.</summary>
        public static HeldItemCommand Limp => Push(Vec2.Zero, 0f);
    }
}
