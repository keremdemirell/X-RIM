using System;

namespace XRim.Simulation.Scenarios
{
    /// <summary>The rules state of a saved board (<see cref="XRim.Rules.Match.MatchState"/>) in plain fields.</summary>
    [Serializable]
    public sealed class MatchStateData
    {
        public int TurnIndex;
        public bool IsSuddenDeath;
        public int SuddenDeathRepeats;
        public FighterStateData Left = new FighterStateData();
        public FighterStateData Right = new FighterStateData();
        public ElectricWallStateData LeftWall = new ElectricWallStateData();
        public ElectricWallStateData RightWall = new ElectricWallStateData();
    }
}
