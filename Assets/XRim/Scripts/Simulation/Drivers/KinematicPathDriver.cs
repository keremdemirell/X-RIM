using XRim.Core;
using XRim.Rules.Settings;
using XRim.Simulation.Physics;
using XRim.Simulation.Settings;

namespace XRim.Simulation.Drivers
{
    /// <summary>
    /// D1 option A (designer, 2026-10-01): the held item is moved exactly onto the path target at the end of every step. Timing
    /// is exact (t = d / v with no lag) and the weapon never loses to what it hits: it shoves bodies aside like an unstoppable
    /// blade. When the rules stop it, it is handed to physics carrying its own speed (D1's second half) and held in the hand
    /// by <paramref name="holdMotor"/>.
    /// </summary>
    public sealed class KinematicPathDriver : PathWeaponDriver
    {
        public KinematicPathDriver(PathSettings paths, IWeaponAimModel aim, WeaponMotorSettings holdMotor = null,
            HitReactionSettings reaction = null, ClashReactionSettings clashReaction = null)
            : base(paths, aim, holdMotor ?? new WeaponMotorSettings(), reaction ?? new HitReactionSettings(),
                clashReaction ?? new ClashReactionSettings())
        {
        }

        protected override HeldItemCommand DriveTowardTarget(SimTime stepStart, SimTime stepEnd, BodyPose torsoAtStart, BodyPose torsoAtEnd,
            BodyState heldItem) =>
            HeldItemCommand.MoveTo(EvaluateTarget(stepEnd, torsoAtEnd));
    }
}
