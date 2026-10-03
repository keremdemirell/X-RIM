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
    /// The hit rules can slow the weapon (D26) or stop it (its last hit, an interrupt).
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

        /// <summary>
        /// Distance travelled along the path at a time, clamped to the path: d = v·t at the weapon's speed, slower after a
        /// slowing hit, frozen once stopped. Used for time-to-impact.
        /// </summary>
        float DistanceAlongPathUnits(SimTime time);

        /// <summary>The attack is over at this time: the path has been run, or the weapon was stopped.</summary>
        bool IsComplete(SimTime time);

        /// <summary>
        /// The weapon is moving along its drawn path at this time: it has a path, has not reached the end and had not been
        /// stopped yet. Only then does it land hits (E2).
        /// </summary>
        bool IsTravelling(SimTime time);

        /// <summary>From this time the weapon continues its path at this share of its own speed (D26: a hit slows it).</summary>
        void SlowTo(SimTime time, float speedFraction);

        /// <summary>
        /// Ends the attack at this time. A weapon moved kinematically until now is handed to physics carrying its own speed
        /// (D1); from then on a motor holds it in the hand: where it stopped, or backed off along its path by the recoil after
        /// its last hit (D26).
        /// </summary>
        void Stop(SimTime time, WeaponStopKind kind);
    }
}
