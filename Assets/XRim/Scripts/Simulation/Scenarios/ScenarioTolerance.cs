namespace XRim.Simulation.Scenarios
{
    /// <summary>
    /// How far a replayed turn may drift from its recorded outcome and still count as the same turn (<see cref="ScenarioComparison"/>).
    /// A replay of the same inputs on the same code is exact; the tolerance absorbs tiny changes in code or float rounding without
    /// letting a different outcome through (a hit landing, missing or changing order is always a difference).
    /// </summary>
    public sealed class ScenarioTolerance
    {
        /// <summary>10 ms: about two 240 Hz physics steps (technical, not a GDD value).</summary>
        public const long DefaultTimeMicroseconds = 10_000L;

        /// <summary>Half a point of damage or HP, half a degree of a clash angle, half a unit of a wall position (technical).</summary>
        public const float DefaultAmount = 0.5f;

        /// <summary>Five arena units of a dummy's final standing point (technical).</summary>
        public const float DefaultPositionUnits = 5f;

        public long TimeMicroseconds { get; }
        public float Amount { get; }
        public float PositionUnits { get; }

        public ScenarioTolerance(long timeMicroseconds, float amount, float positionUnits)
        {
            TimeMicroseconds = timeMicroseconds;
            Amount = amount;
            PositionUnits = positionUnits;
        }

        /// <summary>The regression tests' tolerance.</summary>
        public static ScenarioTolerance Default { get; } = new ScenarioTolerance(DefaultTimeMicroseconds, DefaultAmount, DefaultPositionUnits);

        /// <summary>No drift at all: the same inputs twice must give exactly this.</summary>
        public static ScenarioTolerance Exact { get; } = new ScenarioTolerance(0L, 0f, 0f);
    }
}
