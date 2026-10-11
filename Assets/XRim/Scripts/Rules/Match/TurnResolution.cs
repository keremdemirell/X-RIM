namespace XRim.Rules.Match
{
    public enum TurnResolution
    {
        NextTurn = 0,

        /// <summary>Double KO or the turn cap was reached (GDD §3, §13, §14).</summary>
        EnterSuddenDeath = 1,

        /// <summary>
        /// A sudden-death turn without a winner (nobody hit, a tie replayed, both idle) is played again, both at 1 HP; the repeats are capped
        /// (§14, P6).
        /// </summary>
        RepeatSuddenDeathTurn = 2,
        MatchOver = 3,
    }
}
