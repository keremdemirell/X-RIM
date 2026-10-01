using XRim.Rules;
using XRim.Rules.Settings;

namespace XRim.Simulation.Drivers
{
    /// <summary>
    /// Picks the driver that plays a planned body move (GDD §5). Session 05 supplies one with crouch, lunge, step back
    /// and jump; until then <see cref="NeutralBodyMoveDriverFactory"/> holds every dummy in place.
    /// </summary>
    public interface IBodyMoveDriverFactory
    {
        IBodyMoveDriver Create(BodyMove move, RulesSettings rules, SimulationSettings simulation);
    }
}
