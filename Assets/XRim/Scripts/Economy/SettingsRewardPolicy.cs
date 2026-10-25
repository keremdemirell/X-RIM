using System;
using XRim.Core;
using XRim.Core.Gdd;
using XRim.Rules.Match;

namespace XRim.Economy
{
    /// <summary>
    /// The default reward policy (GDD §16, TBD; designer 2026-10-25): a match win pays <see cref="EconomySettings.RewardPerWin"/> to the
    /// winner and nothing else pays. Paying per hit and per parry are switches (<see cref="EconomySettings.RewardPerHit"/>,
    /// <see cref="EconomySettings.RewardPerParry"/>) set to 0: paying per hit could reward reckless play over winning, so it is not on
    /// until the designer tries it. Reads a <see cref="MatchSummary"/> and nothing else, so it can never reach the rules.
    /// </summary>
    [GddTbd("§16", "Sources and amounts of earned currency", Proposal = "Designer 2026-10-25: a match win only; hits and parries are switches, off")]
    public sealed class SettingsRewardPolicy : IRewardPolicy
    {
        private readonly EconomySettings _settings;

        public SettingsRewardPolicy(EconomySettings settings)
        {
            _settings = Guard.NotNull(settings, nameof(settings));
        }

        public EarnedAmount RewardFor(MatchSummary summary, Side side, bool isFriendMatch)
        {
            Guard.NotNull(summary, nameof(summary));
            long total = 0;
            if (summary.Outcome.Winner == side) total += Math.Max(_settings.RewardPerWin, 0L);
            total += Math.Max(_settings.RewardPerHit, 0L) * summary.HitsLanded[side];
            total += Math.Max(_settings.RewardPerParry, 0L) * summary.SuccessfulParries[side];
            return new EarnedAmount(total);
        }
    }
}
