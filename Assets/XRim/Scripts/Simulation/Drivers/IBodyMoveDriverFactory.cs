using XRim.Rules;
using XRim.Rules.Settings;

namespace XRim.Simulation.Drivers
{
    /// <summary>
    /// Picks the driver that plays a planned body move (GDD §5). The game's moves come from
    /// <see cref="StanceBodyMoveDriverFactory"/>; tests supply their own.
    /// </summary>
    public interface IBodyMoveDriverFactory
    {
        IBodyMoveDriver Create(BodyMove move, RulesSettings rules, SimulationSettings simulation);
    }
}
