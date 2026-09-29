namespace XRim.Rules
{
    /// <summary>
    /// Numbers the GDD marks Decided and NOT tunable. They live in code on purpose: changing one is a
    /// design decision, not a tuning pass. Tunable numbers live in the settings classes instead.
    /// </summary>
    public static class RuleConstants
    {
        /// <summary>GDD §3 and Appendix A: "3 (Decided, not tunable)". Idle turns in a row that lose by forfeit.</summary>
        public const int IdleTurnsBeforeForfeit = 3;

        /// <summary>GDD §14 and Appendix A: "1 (Decided)". Both dummies are set to this HP in sudden death.</summary>
        public const float SuddenDeathHp = 1f;

        /// <summary>GDD §6 (Decided): each player picks 3 loadout slots before a match.</summary>
        public const int LoadoutSlotCount = 3;
    }
}
