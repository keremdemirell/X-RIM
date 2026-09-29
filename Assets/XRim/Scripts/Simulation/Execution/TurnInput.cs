using XRim.Core;
using XRim.Rules.Planning;
using XRim.Rules.Settings;
using XRim.Simulation.Recording;

namespace XRim.Simulation.Execution
{
    /// <summary>Everything a turn depends on: TurnResult = Resolve(board, left plan, right plan, settings).</summary>
    public sealed class TurnInput
    {
        public BoardSnapshot Board { get; }
        public PerSide<TurnPlan> Plans { get; }
        public RulesSettings Rules { get; }
        public SimulationSettings Simulation { get; }

        public TurnInput(BoardSnapshot board, PerSide<TurnPlan> plans, RulesSettings rules, SimulationSettings simulation)
        {
            Board = Guard.NotNull(board, nameof(board));
            Plans = Guard.NotNull(plans, nameof(plans));
            Rules = Guard.NotNull(rules, nameof(rules));
            Simulation = Guard.NotNull(simulation, nameof(simulation));
        }
    }
}
