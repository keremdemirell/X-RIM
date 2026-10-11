namespace XRim.Rules.Arena
{
    /// <summary>
    /// One player's own wall; both can be up at once (GDD §13). It always stands behind its owner, on the side away from the
    /// opponent, its face at <see cref="PositionUnits"/>. <see cref="ElectricWallRules"/> is the only thing that changes it.
    /// <list type="bullet">
    /// <item>Between turns (the frozen board): <see cref="ElectricWallPhase.Warning"/> or <see cref="ElectricWallPhase.Active"/> walls
    /// are up, where they stand; an inactive wall is not there.</item>
    /// <item>From the lock to the end of the turn: <see cref="ElectricWallPhase.Warning"/> means the warning springs up this turn,
    /// when the step back ends (it is not up yet); <see cref="ElectricWallPhase.Active"/> means the wall is up from the turn's start
    /// and zaps its owner on contact.</item>
    /// </list>
    /// </summary>
    public sealed class ElectricWallState
    {
        public ElectricWallPhase Phase { get; set; } = ElectricWallPhase.Inactive;

        /// <summary>True while the wall stands in the arena.</summary>
        public bool IsUp { get; set; }

        /// <summary>Where the wall's face stands along the arena X axis, arena units. Meaningful only while it is up.</summary>
        public float PositionUnits { get; set; }

        /// <summary>Backward swipes in a row, this turn's included: 1 raises the warning, 2 and more zap (§13).</summary>
        public int ConsecutiveRetreats { get; set; }

        /// <summary>No wall: it goes away completely and the sequence starts over (§13).</summary>
        public void Clear()
        {
            Phase = ElectricWallPhase.Inactive;
            IsUp = false;
            PositionUnits = 0f;
            ConsecutiveRetreats = 0;
        }

        public ElectricWallState Clone() => new ElectricWallState
        {
            Phase = Phase,
            IsUp = IsUp,
            PositionUnits = PositionUnits,
            ConsecutiveRetreats = ConsecutiveRetreats,
        };
    }
}
