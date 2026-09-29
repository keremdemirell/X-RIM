using XRim.Core.Gdd;
using XRim.Rules.Match;

namespace XRim.Economy
{
    /// <summary>Stops two friends farming each other in private matches.</summary>
    [GddTbd("§16", "Farming protection", Proposal = "Per-match caps or lower rewards in friend matches")]
    public interface IFarmingGuard
    {
        EarnedAmount Limit(EarnedAmount reward, MatchSummary summary, bool isFriendMatch);
    }
}
