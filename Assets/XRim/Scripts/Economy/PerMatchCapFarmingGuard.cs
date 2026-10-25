using System;
using XRim.Core;
using XRim.Core.Gdd;
using XRim.Rules.Match;

namespace XRim.Economy
{
    /// <summary>
    /// The default farming guard (GDD §16, TBD; designer 2026-10-25): a friend match pays <see cref="EconomySettings.FriendMatchRewardMultiplier"/>
    /// of the reward (rounded down), and one match never pays more than <see cref="EconomySettings.PerMatchRewardCap"/> (0 = no cap).
    /// It can only lower a reward. A cap alone cannot stop two friends trading wins back and forth: that needs a limit per opponent or per
    /// day, which needs a server (Sessions 19–20).
    /// </summary>
    [GddTbd("§16", "Farming protection", Proposal = "Designer 2026-10-25: a per-match cap; lower rewards in friend matches stay a switch")]
    public sealed class PerMatchCapFarmingGuard : IFarmingGuard
    {
        private readonly EconomySettings _settings;

        public PerMatchCapFarmingGuard(EconomySettings settings)
        {
            _settings = Guard.NotNull(settings, nameof(settings));
        }

        public EarnedAmount Limit(EarnedAmount reward, MatchSummary summary, bool isFriendMatch)
        {
            long limited = reward.Value;
            if (isFriendMatch) limited = (long)Math.Floor(limited * (double)Math.Max(_settings.FriendMatchRewardMultiplier, 0f));
            if (_settings.PerMatchRewardCap > 0) limited = Math.Min(limited, _settings.PerMatchRewardCap);
            return new EarnedAmount(Math.Min(limited, reward.Value));
        }
    }
}
