using XRim.Core;

namespace XRim.Rules.Match
{
    /// <summary>
    /// The read-only facts about a finished match that other modules may use (for example Economy rewards).
    /// This is the only thing Economy sees from the rules; combat can never see Economy (GDD §16, pillar 5).
    /// </summary>
    public sealed class MatchSummary
    {
        public MatchOutcome Outcome { get; }
        public PerSide<int> HitsLanded { get; }
        public PerSide<int> SuccessfulParries { get; }

        public MatchSummary(MatchOutcome outcome, PerSide<int> hitsLanded, PerSide<int> successfulParries)
        {
            Outcome = Guard.NotNull(outcome, nameof(outcome));
            HitsLanded = Guard.NotNull(hitsLanded, nameof(hitsLanded));
            SuccessfulParries = Guard.NotNull(successfulParries, nameof(successfulParries));
        }
    }
}
