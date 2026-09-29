using XRim.Core;

namespace XRim.Rules.Match
{
    /// <summary>
    /// What the rules need back from a simulated turn. The simulation layer builds it from its TurnResult,
    /// so the rules never depend on simulation types.
    /// </summary>
    public sealed class ExecutionReport
    {
        /// <summary>The match state after every hit, clash and wall contact of the turn was applied.</summary>
        public MatchState ResolvedState { get; }

        /// <summary>Time of each side's first valid hit, if any. Used by the sudden-death rules (GDD §14).</summary>
        public PerSide<SimTime?> FirstValidHitTime { get; }

        /// <summary>True when both first hits landed on the same simulation step (the TBD exact-tie case).</summary>
        public bool FirstHitsOnSameStep { get; }

        public ExecutionReport(MatchState resolvedState, PerSide<SimTime?> firstValidHitTime, bool firstHitsOnSameStep)
        {
            ResolvedState = Guard.NotNull(resolvedState, nameof(resolvedState));
            FirstValidHitTime = Guard.NotNull(firstValidHitTime, nameof(firstValidHitTime));
            FirstHitsOnSameStep = firstHitsOnSameStep;
        }
    }
}
