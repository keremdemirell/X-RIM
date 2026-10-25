using NUnit.Framework;
using XRim.App;
using XRim.Bots;
using XRim.Core;
using XRim.Economy;

namespace XRim.Tests.EditMode.App
{
    /// <summary>A match as the menu asks for it (Session 18, GDD §16: only a win against the bot pays).</summary>
    public sealed class MatchRequestTests
    {
        [Test]
        public void EachMode_HasItsUsualRequest()
        {
            Assert.That(MatchRequest.For(MatchMode.HotSeat).Kind, Is.EqualTo(MatchKind.HotSeat));
            Assert.That(MatchRequest.For(MatchMode.HumanVsBot).Kind, Is.EqualTo(MatchKind.VersusBot));
            Assert.That(MatchRequest.For(MatchMode.BotVsBot).Kind, Is.EqualTo(MatchKind.Practice));
            Assert.That(MatchRequest.For(MatchMode.Sandbox).Kind, Is.EqualTo(MatchKind.Practice), "the sandbox never pays");
        }

        [Test]
        public void OnlyTheVersusBotMatch_IsOneThePlayerCanEarnIn_ByDefault()
        {
            var economy = new EconomySettings();

            Assert.That(economy.Pays(MatchRequest.VersusBot().Kind), Is.True);
            Assert.That(economy.Pays(MatchRequest.HotSeat().Kind), Is.False);
            Assert.That(economy.Pays(MatchRequest.Practice(BotBrainKind.Heuristic).Kind), Is.False);
            Assert.That(economy.Pays(MatchRequest.WatchBots().Kind), Is.False);
        }

        [Test]
        public void PlaysHumanOn_SaysWhichSidesAPersonPlans()
        {
            Assert.That(MatchRequest.HotSeat().PlaysHumanOn(Side.Left, Side.Left), Is.True);
            Assert.That(MatchRequest.HotSeat().PlaysHumanOn(Side.Right, Side.Left), Is.True);
            Assert.That(MatchRequest.VersusBot().PlaysHumanOn(Side.Left, Side.Left), Is.True);
            Assert.That(MatchRequest.VersusBot().PlaysHumanOn(Side.Right, Side.Left), Is.False);
            Assert.That(MatchRequest.Practice(BotBrainKind.Random).PlaysHumanOn(Side.Left, Side.Left), Is.True);
            Assert.That(MatchRequest.WatchBots().PlaysHumanOn(Side.Left, Side.Left), Is.False);
            Assert.That(MatchRequest.WatchBots().PlaysHumanOn(Side.Right, Side.Left), Is.False);
        }

        [Test]
        public void ApplyTo_GivesEveryBotTheChosenBrain_AndKeepsTheHeuristicsTuning()
        {
            var bots = new BotSettings { VersusBrain = BotBrainKind.Heuristic, WatchLeftBrain = BotBrainKind.Heuristic };

            BotSettings practice = MatchRequest.Practice(BotBrainKind.Random).ApplyTo(bots);

            Assert.That(practice.VersusBrain, Is.EqualTo(BotBrainKind.Random));
            Assert.That(practice.WatchLeftBrain, Is.EqualTo(BotBrainKind.Random));
            Assert.That(practice.WatchRightBrain, Is.EqualTo(BotBrainKind.Random));
            Assert.That(practice.Heuristic, Is.SameAs(bots.Heuristic));
            Assert.That(bots.VersusBrain, Is.EqualTo(BotBrainKind.Heuristic), "the asset's own settings are not changed");
        }

        [Test]
        public void ApplyTo_WithoutAChosenBrain_LeavesTheBotsAssetAlone()
        {
            var bots = new BotSettings { VersusBrain = BotBrainKind.Random };

            Assert.That(MatchRequest.VersusBot().ApplyTo(bots), Is.SameAs(bots));
            Assert.That(MatchRequest.WatchBots().ApplyTo(bots), Is.SameAs(bots));
        }
    }
}
