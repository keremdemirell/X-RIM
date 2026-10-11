namespace XRim.Rules.Match
{
    /// <summary>A match ends only when a dummy dies (GDD §3). Rounds and points wins are Rejected.</summary>
    public enum MatchEndReason
    {
        /// <summary>The other dummy reached 0 HP: a hit, or (P2) its own electric wall.</summary>
        KnockOut = 0,

        /// <summary><see cref="RuleConstants.IdleTurnsBeforeForfeit"/> idle turns in a row.</summary>
        Forfeit = 1,

        /// <summary>First valid hit in sudden death, or the earlier time-to-impact when both hit.</summary>
        SuddenDeathHit = 2,

        /// <summary>
        /// Sudden death ran out of repeats without a winner and this dummy had more HP when it began (§14 TBD fallback, designer 2026-10-11;
        /// it bends §3's "no points win", so it is flagged in <c>CappedRepeatNoHitPolicy</c>).
        /// </summary>
        SuddenDeathMoreHp = 3,

        /// <summary>As <see cref="SuddenDeathMoreHp"/>, but both had the same HP: a coin toss from the match seed decided.</summary>
        SuddenDeathCoinToss = 4,
    }
}
