using XRim.Core;
using XRim.Rules.Settings;
using XRim.Simulation.Physics;

namespace XRim.Simulation.Drivers
{
    /// <summary>
    /// D1 option A: the held item is moved exactly onto the path target at the end of every step. Timing is exact
    /// (t = d / v with no lag) and the weapon never loses to what it hits: it shoves bodies aside like an unstoppable
    /// blade. Whatever it hits reacts; the weapon itself never recoils.
    /// </summary>
    public sealed class KinematicPathDriver : PathWeaponDriver
    {
        public KinematicPathDriver(PathSettings paths, IWeaponAimModel aim) : base(paths, aim)
        {
        }

        protected override HeldItemCommand DriveTowardTarget(SimTime stepStart, SimTime stepEnd, BodyPose torso, BodyState heldItem) =>
            HeldItemCommand.MoveTo(EvaluateTarget(stepEnd, torso));
    }
}
