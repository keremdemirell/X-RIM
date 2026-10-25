namespace XRim.Economy
{
    /// <summary>What a finished match paid, for the match-over screen ("+100 BOLTS") and for tests.</summary>
    public readonly struct RewardReceipt
    {
        public MatchKind Kind { get; }

        /// <summary>What the reward policy asked for, before the farming guard.</summary>
        public EarnedAmount Proposed { get; }

        /// <summary>What was added to the wallet.</summary>
        public EarnedAmount Paid { get; }

        public RewardDecision Decision { get; }

        /// <summary>The farming guard paid less than the policy asked for.</summary>
        public bool WasCapped => Paid < Proposed;

        public RewardReceipt(MatchKind kind, EarnedAmount proposed, EarnedAmount paid, RewardDecision decision)
        {
            Kind = kind;
            Proposed = proposed;
            Paid = paid;
            Decision = decision;
        }
    }
}
