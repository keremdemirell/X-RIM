namespace XRim.Rules.Match
{
    public readonly struct EndCheckResult
    {
        public TurnResolution Resolution { get; }

        /// <summary>Set only when <see cref="Resolution"/> is MatchOver.</summary>
        public MatchOutcome Outcome { get; }

        public EndCheckResult(TurnResolution resolution, MatchOutcome outcome)
        {
            Resolution = resolution;
            Outcome = outcome;
        }
    }
}
