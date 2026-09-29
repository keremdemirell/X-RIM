namespace XRim.Rules.Match
{
    /// <summary>
    /// Authoritative match flow (GDD §3):
    /// MatchSetup → TurnStart → Planning → Locked → Executing → Resolving → (TurnStart | SuddenDeathSetup | MatchOver).
    /// Planning ends as soon as both players are Ready or the timer ends, whichever comes first (Decided).
    /// </summary>
    public enum MatchPhase
    {
        /// <summary>Loadouts are picked and both are revealed to both players (GDD §6).</summary>
        MatchSetup = 0,
        TurnStart = 1,
        Planning = 2,

        /// <summary>Plans are frozen and validated; the electric wall reacts to the planned body moves.</summary>
        Locked = 3,

        /// <summary>The turn is simulated (locally or by a server) and played back.</summary>
        Executing = 4,

        /// <summary>HP, limbs, walls, idle counters and statuses are applied; end conditions are checked.</summary>
        Resolving = 5,

        /// <summary>Both dummies go to 1 HP; board state per the TBD sudden-death board mode (GDD §14).</summary>
        SuddenDeathSetup = 6,
        MatchOver = 7,
    }
}
