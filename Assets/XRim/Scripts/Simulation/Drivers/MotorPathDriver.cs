using XRim.Core;
using XRim.Rules.Settings;
using XRim.Simulation.Physics;
using XRim.Simulation.Settings;

namespace XRim.Simulation.Drivers
{
    /// <summary>
    /// D1 option B: a spring-damper (<see cref="WeaponMotorControl"/>) chases the same path target the kinematic driver uses,
    /// with the target's own velocity fed forward so it tracks without lag on a clear path. It is strength-limited, so an
    /// impact can slow, bounce or deflect the weapon: heavier, more physical hits, at the cost of exact timing after a collision.
    /// </summary>
    public sealed class MotorPathDriver : PathWeaponDriver
    {
        private readonly WeaponMotorSettings _motor;

        public MotorPathDriver(PathSettings paths, WeaponMotorSettings motor, IWeaponAimModel aim, HitReactionSettings reaction = null,
            ClashReactionSettings clashReaction = null)
            : base(paths, aim, Guard.NotNull(motor, nameof(motor)), reaction ?? new HitReactionSettings(),
                clashReaction ?? new ClashReactionSettings())
        {
            _motor = motor;
        }

        protected override HeldItemCommand DriveTowardTarget(SimTime stepStart, SimTime stepEnd, BodyPose torsoAtStart, BodyPose torsoAtEnd,
            BodyState heldItem) =>
            WeaponMotorControl.Toward(EvaluateTarget(stepStart, torsoAtStart), EvaluateTarget(stepEnd, torsoAtEnd),
                (float)(stepEnd - stepStart).Seconds, heldItem, _motor);
    }
}
