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

        /// <summary>True when both first hits landed on the same simulation step (informational: sudden death compares the times, P4).</summary>
        public bool FirstHitsOnSameStep { get; }

        /// <summary>
        /// When each side died this turn, if it did (its <c>FighterDiedEvent</c>). Sudden death needs it for a death without a hit (the
        /// electric wall, P4); null for a side that lived, or when the source of the report does not know.
        /// </summary>
        public PerSide<SimTime?> DeathTime { get; }

        public ExecutionReport(MatchState resolvedState, PerSide<SimTime?> firstValidHitTime, bool firstHitsOnSameStep,
            PerSide<SimTime?> deathTime = null)
        {
            ResolvedState = Guard.NotNull(resolvedState, nameof(resolvedState));
            FirstValidHitTime = Guard.NotNull(firstValidHitTime, nameof(firstValidHitTime));
            FirstHitsOnSameStep = firstHitsOnSameStep;
            DeathTime = deathTime ?? new PerSide<SimTime?>(null, null);
        }
    }
}
