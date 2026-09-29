namespace XRim.Rules.Arena
{
    /// <summary>One player's own wall; both can be active at once (GDD §13).</summary>
    public sealed class ElectricWallState
    {
        public ElectricWallPhase Phase { get; set; } = ElectricWallPhase.Inactive;

        /// <summary>Wall position along the arena X axis, arena units.</summary>
        public float PositionUnits { get; set; }

        public int ConsecutiveRetreats { get; set; }

        public ElectricWallState Clone() => new ElectricWallState
        {
            Phase = Phase,
            PositionUnits = PositionUnits,
            ConsecutiveRetreats = ConsecutiveRetreats,
        };
    }
}
