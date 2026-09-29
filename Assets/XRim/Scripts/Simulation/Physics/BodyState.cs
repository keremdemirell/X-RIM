using XRim.Core;

namespace XRim.Simulation.Physics
{
    /// <summary>Where a physics body is and how it is moving, in arena units. Read by drivers that push bodies.</summary>
    public readonly struct BodyState
    {
        public BodyPose Pose { get; }
        public Vec2 LinearVelocityUnitsPerSecond { get; }

        /// <summary>Counter-clockwise positive.</summary>
        public float AngularVelocityDegreesPerSecond { get; }

        public BodyState(BodyPose pose, Vec2 linearVelocityUnitsPerSecond, float angularVelocityDegreesPerSecond)
        {
            Pose = pose;
            LinearVelocityUnitsPerSecond = linearVelocityUnitsPerSecond;
            AngularVelocityDegreesPerSecond = angularVelocityDegreesPerSecond;
        }
    }
}
