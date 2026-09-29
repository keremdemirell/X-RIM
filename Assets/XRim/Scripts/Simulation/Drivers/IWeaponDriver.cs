using XRim.Core;
using XRim.Rules.Paths;
using XRim.Rules.Settings;
using XRim.Simulation.Physics;

namespace XRim.Simulation.Drivers
{
    /// <summary>
    /// Moves a held item along its path at the weapon's own speed (GDD §9, Decided: t = d / v). The path is in
    /// the torso frame and travels with the body (§6). Implementations are a feel choice (pillar 1): for example
    /// kinematic path following vs a physics motor chasing the path target.
    /// </summary>
    public interface IWeaponDriver
    {
        void Begin(Side side, WeaponPath path, WeaponStats weapon);

        /// <summary>Target pose of the held item at a simulation time, given the torso's current pose.</summary>
        BodyPose EvaluateTarget(SimTime time, BodyPose torso);

        /// <summary>Distance travelled along the path at a time. Used to refine time-to-impact within a step.</summary>
        float DistanceAlongPathUnits(SimTime time);

        bool IsComplete(SimTime time);

        /// <summary>Stops the attack (interrupted, knocked off its path, or rebounded).</summary>
        void Cancel();
    }
}
