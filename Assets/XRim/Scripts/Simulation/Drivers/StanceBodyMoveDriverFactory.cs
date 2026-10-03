using XRim.Rules;
using XRim.Rules.Settings;

namespace XRim.Simulation.Drivers
{
    /// <summary>
    /// The game's body moves (GDD §5): no move holds the pose (<see cref="NeutralBodyMoveDriver"/>), and every stance swipe
    /// plays from its data (<see cref="StanceBodyMoveDriver"/>). A move the tuning profile has no data for holds the pose.
    /// </summary>
    public sealed class StanceBodyMoveDriverFactory : IBodyMoveDriverFactory
    {
        public IBodyMoveDriver Create(BodyMove move, RulesSettings rules, SimulationSettings simulation)
        {
            if (move == BodyMove.None || rules == null || rules.FindBodyMove(move) == null) return new NeutralBodyMoveDriver();
            return new StanceBodyMoveDriver();
        }
    }
}
