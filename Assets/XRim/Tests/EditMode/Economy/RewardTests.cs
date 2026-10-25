using NUnit.Framework;
using XRim.Core;
using XRim.Economy;
using XRim.Rules.Match;

namespace XRim.Tests.EditMode.Economy
{
    /// <summary>GDD §16 (TBD, designer 2026-10-25): a match win pays, hits and parries are switches, a per-match cap guards farming.</summary>
    public sealed class RewardTests
    {
        private EconomySettings _settings;
        private Wallet _wallet;

        [SetUp]
        public void SetUp()
        {
            _settings = new EconomySettings();
            _wallet = new Wallet();
        }

        private static MatchSummary Summary(Side winner, int leftHits = 0, int rightHits = 0, int leftParries = 0, int rightParries = 0) =>
            new MatchSummary(new MatchOutcome(winner, MatchEndReason.KnockOut, 9), new PerSide<int>(leftHits, rightHits),
                new PerSide<int>(leftParries, rightParries));

        private RewardReceipt Settle(MatchSummary summary, Side side, MatchKind kind = MatchKind.VersusBot, bool friend = false) =>
            RewardSettlement.CreateDefault(_settings).Settle(_wallet, summary, side, kind, friend);

        [Test]
        public void Default_AMatchWinPaysTheWinner()
        {
            RewardReceipt receipt = Settle(Summary(Side.Left), Side.Left);

            Assert.That(receipt.Decision, Is.EqualTo(RewardDecision.Paid));
            Assert.That(receipt.Paid.Value, Is.EqualTo(_settings.RewardPerWin));
            Assert.That(_wallet.Earned.Value, Is.EqualTo(_settings.RewardPerWin));
        }

        [Test]
        public void Default_TheLoserEarnsNothing()
        {
            RewardReceipt receipt = Settle(Summary(Side.Left, leftHits: 6, rightHits: 6, rightParries: 4), Side.Right);

            Assert.That(receipt.Decision, Is.EqualTo(RewardDecision.NothingEarned));
            Assert.That(_wallet.Earned.Value, Is.EqualTo(0L), "Hits and parries pay nothing unless the designer switches them on");
        }

        [Test]
        public void Default_HitsAndParriesDoNotChangeAWinsReward()
        {
            Settle(Summary(Side.Right, leftHits: 1, rightHits: 12, rightParries: 9), Side.Right);

            Assert.That(_wallet.Earned.Value, Is.EqualTo(_settings.RewardPerWin));
        }

        [Test]
        public void HitAndParryPayouts_WhenSwitchedOn_PayTheSideThatMadeThem()
        {
            _settings.RewardPerWin = 100;
            _settings.RewardPerHit = 5;
            _settings.RewardPerParry = 7;
            _settings.PerMatchRewardCap = 0;

            EarnedAmount winner = new SettingsRewardPolicy(_settings).RewardFor(Summary(Side.Left, 4, 3, 2, 1), Side.Left, false);
            EarnedAmount loser = new SettingsRewardPolicy(_settings).RewardFor(Summary(Side.Left, 4, 3, 2, 1), Side.Right, false);

            Assert.That(winner.Value, Is.EqualTo(100L + 4 * 5 + 2 * 7));
            Assert.That(loser.Value, Is.EqualTo(3L * 5 + 1 * 7), "A loser is paid for hits and parries only, never the win");
        }

        [Test]
        public void NegativeSettings_NeverTakeCurrencyAway()
        {
            _settings.RewardPerWin = -50;
            _settings.RewardPerHit = -5;

            RewardReceipt receipt = Settle(Summary(Side.Left, leftHits: 3), Side.Left);

            Assert.That(receipt.Paid.Value, Is.EqualTo(0L));
            Assert.That(_wallet.Earned.Value, Is.EqualTo(0L));
        }

        [Test]
        public void TheCap_LimitsWhatOneMatchPays()
        {
            _settings.RewardPerWin = 100;
            _settings.RewardPerHit = 20;
            _settings.PerMatchRewardCap = 150;

            RewardReceipt receipt = Settle(Summary(Side.Left, leftHits: 10), Side.Left);

            Assert.That(receipt.Proposed.Value, Is.EqualTo(300L));
            Assert.That(receipt.Paid.Value, Is.EqualTo(150L));
            Assert.That(receipt.WasCapped, Is.True);
            Assert.That(_wallet.Earned.Value, Is.EqualTo(150L));
        }

        [Test]
        public void ACapOfZero_MeansNoCap()
        {
            _settings.RewardPerWin = 100000;
            _settings.PerMatchRewardCap = 0;

            Assert.That(Settle(Summary(Side.Left), Side.Left).Paid.Value, Is.EqualTo(100000L));
        }

        [Test]
        public void AFriendMatch_PaysTheFriendShareRoundedDown()
        {
            _settings.RewardPerWin = 101;
            _settings.PerMatchRewardCap = 0;
            _settings.FriendMatchRewardMultiplier = 0.5f;

            Assert.That(Settle(Summary(Side.Left), Side.Left, friend: true).Paid.Value, Is.EqualTo(50L));
            Assert.That(Settle(Summary(Side.Left), Side.Left, friend: false).Paid.Value, Is.EqualTo(101L), "Other matches are not scaled");
        }

        [Test]
        public void AFriendMatchMultiplierBelowZero_PaysNothing()
        {
            _settings.FriendMatchRewardMultiplier = -3f;

            Assert.That(Settle(Summary(Side.Left), Side.Left, friend: true).Paid.Value, Is.EqualTo(0L));
        }

        [Test]
        public void Default_OnlyAWinAgainstTheBotPays()
        {
            Assert.That(Settle(Summary(Side.Left), Side.Left, MatchKind.HotSeat).Decision, Is.EqualTo(RewardDecision.ModeDoesNotPay));
            Assert.That(Settle(Summary(Side.Left), Side.Left, MatchKind.Practice).Decision, Is.EqualTo(RewardDecision.ModeDoesNotPay));
            Assert.That(_wallet.Earned.Value, Is.EqualTo(0L));

            Assert.That(Settle(Summary(Side.Left), Side.Left, MatchKind.VersusBot).Decision, Is.EqualTo(RewardDecision.Paid));
        }

        [Test]
        public void ModesCanBeSwitchedOn()
        {
            _settings.PaysInHotSeat = true;

            Assert.That(Settle(Summary(Side.Left), Side.Left, MatchKind.HotSeat).Decision, Is.EqualTo(RewardDecision.Paid));
        }

        [Test]
        public void PremiumCurrency_IsNeverPaid_EvenIfThePremiumFlagIsOn()
        {
            _settings.PremiumEarnableInPlay = true;

            Settle(Summary(Side.Left, leftHits: 5, leftParries: 5), Side.Left);

            Assert.That(_wallet.Premium.Value, Is.EqualTo(0L), "No source of premium currency exists (§16, designer 2026-10-25)");
        }

        [Test]
        public void TheSettlementUsesTheSeams_NotTheDefaults()
        {
            var settlement = new RewardSettlement(_settings, new FixedPolicy(500), new PerMatchCapFarmingGuard(_settings));

            RewardReceipt receipt = settlement.Settle(_wallet, Summary(Side.Left), Side.Left, MatchKind.VersusBot, false);

            Assert.That(receipt.Proposed.Value, Is.EqualTo(500L), "The policy decides what is asked");
            Assert.That(receipt.Paid.Value, Is.EqualTo(_settings.PerMatchRewardCap), "The guard decides what is let through");
        }

        [Test]
        public void AFarmingGuardCanOnlyLowerAReward()
        {
            var settlement = new RewardSettlement(_settings, new FixedPolicy(40), new GenerousGuard());

            RewardReceipt receipt = settlement.Settle(_wallet, Summary(Side.Left), Side.Left, MatchKind.VersusBot, false);

            Assert.That(receipt.Paid.Value, Is.EqualTo(40L));
            Assert.That(_wallet.Earned.Value, Is.EqualTo(40L));
        }

        [Test]
        public void ThePolicy_ReadsOnlyTheSummary()
        {
            // The only inputs are a MatchSummary, a side and a flag: nothing of the rules, the simulation or the physics can be reached.
            var parameters = typeof(IRewardPolicy).GetMethod(nameof(IRewardPolicy.RewardFor)).GetParameters();

            Assert.That(parameters.Length, Is.EqualTo(3));
            Assert.That(parameters[0].ParameterType, Is.EqualTo(typeof(MatchSummary)));
        }

        private sealed class FixedPolicy : IRewardPolicy
        {
            private readonly long _amount;

            public FixedPolicy(long amount)
            {
                _amount = amount;
            }

            public EarnedAmount RewardFor(MatchSummary summary, Side side, bool isFriendMatch) => new EarnedAmount(_amount);
        }

        private sealed class GenerousGuard : IFarmingGuard
        {
            public EarnedAmount Limit(EarnedAmount reward, MatchSummary summary, bool isFriendMatch) => new EarnedAmount(reward.Value * 10);
        }
    }
}
