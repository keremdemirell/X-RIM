using XRim.Core;
using XRim.Rules.Settings;
using XRim.Simulation.Physics;
using XRim.Simulation.Settings;

namespace XRim.Simulation.Drivers
{
    /// <summary>
    /// D1 option B: a spring-damper (PD) chases the same path target the kinematic driver uses, with the target's own
    /// velocity fed forward so it tracks without lag on a clear path. It is strength-limited, so an impact can slow,
    /// bounce or deflect the weapon: heavier, more physical hits, at the cost of exact timing after a collision.
    /// </summary>
    public sealed class MotorPathDriver : PathWeaponDriver
    {
        private readonly WeaponMotorSettings _motor;

        public MotorPathDriver(PathSettings paths, WeaponMotorSettings motor, IWeaponAimModel aim) : base(paths, aim)
        {
            _motor = Guard.NotNull(motor, nameof(motor));
        }

        protected override HeldItemCommand DriveTowardTarget(SimTime stepStart, SimTime stepEnd, BodyPose torso, BodyState heldItem)
        {
            float stepSeconds = (float)(stepEnd - stepStart).Seconds;
            if (stepSeconds <= 0f) return HeldItemCommand.Limp;

            BodyPose now = EvaluateTarget(stepStart, torso);
            BodyPose next = EvaluateTarget(stepEnd, torso);
            Vec2 targetVelocity = (next.PositionUnits - now.PositionUnits) / stepSeconds;
            float targetAngularVelocity = XMath.DeltaAngleDegrees(now.RotationDegrees, next.RotationDegrees) / stepSeconds;

            Vec2 acceleration =
                (now.PositionUnits - heldItem.Pose.PositionUnits) * Stiffness(_motor.FrequencyHz) +
                (targetVelocity - heldItem.LinearVelocityUnitsPerSecond) * Damping(_motor.FrequencyHz, _motor.DampingRatio);
            float angularAcceleration =
                XMath.DeltaAngleDegrees(heldItem.Pose.RotationDegrees, now.RotationDegrees) * Stiffness(_motor.AngularFrequencyHz) +
                (targetAngularVelocity - heldItem.AngularVelocityDegreesPerSecond) *
                Damping(_motor.AngularFrequencyHz, _motor.AngularDampingRatio);

            return HeldItemCommand.Push(
                ClampLength(acceleration, _motor.MaxAccelerationUnitsPerSecondSquared),
                ClampMagnitude(angularAcceleration, _motor.MaxAngularAccelerationDegreesPerSecondSquared));
        }

        private static float Stiffness(float frequencyHz)
        {
            float omega = XMath.TwoPi * frequencyHz;
            return omega * omega;
        }

        private static float Damping(float frequencyHz, float dampingRatio) => 2f * dampingRatio * XMath.TwoPi * frequencyHz;

        /// <summary>A limit of 0 means unlimited.</summary>
        private static Vec2 ClampLength(Vec2 vector, float limit) =>
            limit > 0f && vector.Length > limit ? vector.Normalized * limit : vector;

        private static float ClampMagnitude(float value, float limit) => limit > 0f ? XMath.Clamp(value, -limit, limit) : value;
    }
}
