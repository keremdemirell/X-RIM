using System;
using XRim.Rules.Arena;

namespace XRim.Simulation.Scenarios
{
    /// <summary>One player's electric wall (<see cref="ElectricWallState"/>, GDD §13) in plain fields.</summary>
    [Serializable]
    public sealed class ElectricWallStateData
    {
        public ElectricWallPhase Phase;
        public bool IsUp;
        public float PositionUnits;
        public int ConsecutiveRetreats;
    }
}
