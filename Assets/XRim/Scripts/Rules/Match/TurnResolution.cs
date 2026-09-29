namespace XRim.Rules.Match
{
    public enum TurnResolution
    {
        NextTurn = 0,

        /// <summary>Double KO or the turn cap was reached (GDD §3, §13, §14).</summary>
        EnterSuddenDeath = 1,

        /// <summary>No hit, or an exact same-step tie, in sudden death (policies are TBD).</summary>
        RepeatSuddenDeathTurn = 2,
        MatchOver = 3,
    }
}
