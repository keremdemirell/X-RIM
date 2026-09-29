using XRim.Core;
using XRim.Core.Gdd;
using XRim.Rules.Match;

namespace XRim.Economy
{
    /// <summary>
    /// Earned currency paid for a finished match. These rewards are unrelated to any in-match weapon trait (§15).
    /// Candidate sources: successful hits, successful parries, match wins; the designer is unsure about hits and parries.
    /// </summary>
    [GddTbd("§16", "Sources and amounts of earned currency")]
    public interface IRewardPolicy
    {
        EarnedAmount RewardFor(MatchSummary summary, Side side, bool isFriendMatch);
    }
}
