using XRim.Core.Gdd;

namespace XRim.Rules.Match
{
    /// <summary>What happens when both players reach the forfeit limit in the same turn (not covered by the GDD).</summary>
    [GddTbd("§3", "Both players forfeit in the same turn", Proposal = "Sudden death, like a double KO")]
    public enum BothForfeitRule
    {
        /// <summary>Nobody can be named the winner, so both go to sudden death, like a double KO. Designer's pick, 2026-10-01.</summary>
        SuddenDeath = 0,

        /// <summary>The dummy with more HP wins by forfeit. Equal HP goes to sudden death.</summary>
        HigherHpWins = 1,
    }
}
