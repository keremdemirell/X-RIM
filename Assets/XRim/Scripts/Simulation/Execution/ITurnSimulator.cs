using XRim.Simulation.Recording;

namespace XRim.Simulation.Execution
{
    /// <summary>Resolves one turn up front, faster than real time. The result is then played back.</summary>
    public interface ITurnSimulator
    {
        TurnResult Simulate(TurnInput input);
    }
}
