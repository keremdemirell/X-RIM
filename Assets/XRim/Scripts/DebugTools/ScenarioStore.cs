using System;
using XRim.Core;
using XRim.Rules.Planning;
using XRim.Simulation.Recording;

namespace XRim.DebugTools
{
    /// <summary>
    /// Saves and loads a scenario: a frozen board plus both players' plans. Because a turn is a pure function of these,
    /// a saved scenario replays exactly and can become a regression test or a feel-tuning fixture.
    /// </summary>
    public sealed class ScenarioStore
    {
        public void Save(string path, BoardSnapshot board, PerSide<TurnPlan> plans)
        {
            // Placeholder: architecture setup only. Needs serializable DTOs for plans and match state.
            throw new NotImplementedException("ScenarioStore.Save is not implemented yet.");
        }

        public (BoardSnapshot Board, PerSide<TurnPlan> Plans) Load(string path)
        {
            // Placeholder: architecture setup only.
            throw new NotImplementedException("ScenarioStore.Load is not implemented yet.");
        }
    }
}
