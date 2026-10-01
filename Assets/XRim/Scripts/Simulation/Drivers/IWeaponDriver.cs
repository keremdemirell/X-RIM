using XRim.Core;
using XRim.Rules.Paths;
using XRim.Rules.Settings;
using XRim.Simulation.Physics;

namespace XRim.Simulation.Drivers
{
    /// <summary>
    /// Moves a held item along its path at the weapon's own speed (GDD §9, Decided: t = d / v). The path is in
    /// the torso frame and travels with the body (§6). Implementations are a feel choice (pillar 1, D1):
    /// kinematic path following (<see cref="KinematicPathDriver"/>) or a motor chasing the path (<see cref="MotorPathDriver"/>).
    /// </summary>
    public interface IWeaponDriver
    {
        /// <summary>Starts a swing at simulation time zero. The path's first point is the weapon tip's start (D3 lead-in).</summary>
        void Begin(Side side, WeaponPath path, WeaponStats weapon);

        /// <summary>Where the held item should be at a time (arena units), given the torso frame's current pose.</summary>
        BodyPose EvaluateTarget(SimTime time, BodyPose torso);

        /// <summary>
        /// What the physics world should do with the held item during the step from <paramref name="stepStart"/> to
        /// <paramref name="stepEnd"/>. The torso frame is given at both ends, because a body move carries it during the step.
        /// </summary>
        HeldItemCommand Drive(SimTime stepStart, SimTime stepEnd, BodyPose torsoAtStart, BodyPose torsoAtEnd, BodyState heldItem);

        /// <summary>Distance travelled along the path at a time: d = v·t, clamped to the path. Used for time-to-impact.</summary>
        float DistanceAlongPathUnits(SimTime time);

        bool IsComplete(SimTime time);

        /// <summary>Stops the attack (interrupted, knocked off its path, or rebounded). The held item goes limp.</summary>
        void Cancel();
    }
}
