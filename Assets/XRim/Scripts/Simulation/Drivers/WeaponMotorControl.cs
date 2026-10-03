using XRim.Core;
using XRim.Simulation.Physics;
using XRim.Simulation.Settings;

namespace XRim.Simulation.Drivers
{
    /// <summary>
    /// The spring-damper (PD) that brings a free held item onto a moving target, with the target's own velocity fed forward
    /// so it tracks without lag. The motor driver (D1 option B) chases its path with it, and every driver holds a stopped
    /// weapon in the hand with it. Mass-free and strength-limited, so an impact can still slow or bounce the weapon.
    /// </summary>
    public static class WeaponMotorControl
    {
        /// <param name="now">The target at the start of the step.</param>
        /// <param name="next">The target at the end of the step.</param>
        public static HeldItemCommand Toward(BodyPose now, BodyPose next, float stepSeconds, BodyState heldItem, WeaponMotorSettings motor)
        {
            Guard.NotNull(motor, nameof(motor));
            if (stepSeconds <= 0f) return HeldItemCommand.Limp;

            Vec2 targetVelocity = (next.PositionUnits - now.PositionUnits) / stepSeconds;
            float targetAngularVelocity = XMath.DeltaAngleDegrees(now.RotationDegrees, next.RotationDegrees) / stepSeconds;

            Vec2 acceleration =
                (now.PositionUnits - heldItem.Pose.PositionUnits) * Stiffness(motor.FrequencyHz) +
                (targetVelocity - heldItem.LinearVelocityUnitsPerSecond) * Damping(motor.FrequencyHz, motor.DampingRatio);
            float angularAcceleration =
                XMath.DeltaAngleDegrees(heldItem.Pose.RotationDegrees, now.RotationDegrees) * Stiffness(motor.AngularFrequencyHz) +
                (targetAngularVelocity - heldItem.AngularVelocityDegreesPerSecond) *
                Damping(motor.AngularFrequencyHz, motor.AngularDampingRatio);

            return HeldItemCommand.Push(
                ClampLength(acceleration, motor.MaxAccelerationUnitsPerSecondSquared),
                ClampMagnitude(angularAcceleration, motor.MaxAngularAccelerationDegreesPerSecondSquared));
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
