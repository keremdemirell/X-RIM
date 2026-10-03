using XRim.Core;
using XRim.Rules.Settings;
using XRim.Simulation.Settings;

namespace XRim.Simulation.Drivers
{
    /// <summary>Builds the weapon driver the settings ask for (D1), so it can be switched live.</summary>
    public static class WeaponDriverFactory
    {
        public static IWeaponDriver Create(SimulationSettings simulation, PathSettings paths, IWeaponAimModel aim)
        {
            Guard.NotNull(simulation, nameof(simulation));
            return simulation.WeaponDriver == WeaponDriverKind.Motor
                ? new MotorPathDriver(paths, simulation.WeaponMotor, aim, simulation.HitReaction, simulation.ClashReaction)
                : (IWeaponDriver)new KinematicPathDriver(paths, aim, simulation.WeaponMotor, simulation.HitReaction, simulation.ClashReaction);
        }
    }
}
