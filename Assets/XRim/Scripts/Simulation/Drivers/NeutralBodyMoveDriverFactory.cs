using XRim.Rules;
using XRim.Rules.Settings;

namespace XRim.Simulation.Drivers
{
    /// <summary>Session 04 placeholder: every planned body move plays as no move, until Session 05 adds the real ones.</summary>
    public sealed class NeutralBodyMoveDriverFactory : IBodyMoveDriverFactory
    {
        public IBodyMoveDriver Create(BodyMove move, RulesSettings rules, SimulationSettings simulation) => new NeutralBodyMoveDriver();
    }
}
