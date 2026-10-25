using XRim.Core;
using XRim.Rules.Match;

namespace XRim.Economy
{
    /// <summary>
    /// Pays a finished match into a wallet (GDD §16): does this kind of match pay at all, what the <see cref="IRewardPolicy"/> asks for,
    /// what the <see cref="IFarmingGuard"/> lets through (never more than asked). Only earned currency is ever paid: no source of premium
    /// currency exists, whatever <see cref="EconomySettings.PremiumEarnableInPlay"/> says. It reads a <see cref="MatchSummary"/> and
    /// nothing else from the match.
    /// </summary>
    public sealed class RewardSettlement
    {
        private readonly EconomySettings _settings;
        private readonly IRewardPolicy _policy;
        private readonly IFarmingGuard _guard;

        public RewardSettlement(EconomySettings settings, IRewardPolicy policy, IFarmingGuard guard)
        {
            _settings = Guard.NotNull(settings, nameof(settings));
            _policy = Guard.NotNull(policy, nameof(policy));
            _guard = Guard.NotNull(guard, nameof(guard));
        }

        /// <summary>The default policy and guard over these settings (the flagged defaults of Session 18).</summary>
        public static RewardSettlement CreateDefault(EconomySettings settings) =>
            new RewardSettlement(settings, new SettingsRewardPolicy(settings), new PerMatchCapFarmingGuard(settings));

        /// <param name="side">The player being paid.</param>
        public RewardReceipt Settle(Wallet wallet, MatchSummary summary, Side side, MatchKind kind, bool isFriendMatch)
        {
            Guard.NotNull(wallet, nameof(wallet));
            Guard.NotNull(summary, nameof(summary));
            if (!_settings.Pays(kind)) return new RewardReceipt(kind, EarnedAmount.Zero, EarnedAmount.Zero, RewardDecision.ModeDoesNotPay);

            EarnedAmount proposed = _policy.RewardFor(summary, side, isFriendMatch);
            EarnedAmount limited = _guard.Limit(proposed, summary, isFriendMatch);
            EarnedAmount paid = limited > proposed ? proposed : limited;
            if (paid.Value == 0) return new RewardReceipt(kind, proposed, paid, RewardDecision.NothingEarned);

            wallet.Add(paid);
            return new RewardReceipt(kind, proposed, paid, RewardDecision.Paid);
        }
    }
}
