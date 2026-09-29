namespace XRim.Rules.Match
{
    /// <summary>A match ends only when a dummy dies (GDD §3). Rounds and points wins are Rejected.</summary>
    public enum MatchEndReason
    {
        KnockOut = 0,

        /// <summary><see cref="RuleConstants.IdleTurnsBeforeForfeit"/> idle turns in a row.</summary>
        Forfeit = 1,

        /// <summary>First valid hit in sudden death, or the earlier time-to-impact when both hit.</summary>
        SuddenDeathHit = 2,
    }
}
