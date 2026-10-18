namespace XRim.Bots.Balance
{
    /// <summary>What the batch measures against (read by the caller from the Balance Targets asset): never a rule.</summary>
    public sealed class BalanceTargets
    {
        public BalanceTargets(float dismembermentMatchRateMin, float dismembermentMatchRateMax, float targetMatchLengthSeconds,
            float assumedPlanningSecondsPerTurn)
        {
            DismembermentMatchRateMin = dismembermentMatchRateMin;
            DismembermentMatchRateMax = dismembermentMatchRateMax;
            TargetMatchLengthSeconds = targetMatchLengthSeconds;
            AssumedPlanningSecondsPerTurn = assumedPlanningSecondsPerTurn;
        }

        /// <summary>GDD §11: severing should happen in about 10–20% of matches.</summary>
        public float DismembermentMatchRateMin { get; }
        public float DismembermentMatchRateMax { get; }

        /// <summary>GDD §1: TBD; 0 means not decided, so the report measures and does not judge.</summary>
        public float TargetMatchLengthSeconds { get; }

        /// <summary>How long a person is assumed to plan each turn, for the match-length estimate (a placeholder).</summary>
        public float AssumedPlanningSecondsPerTurn { get; }
    }
}
