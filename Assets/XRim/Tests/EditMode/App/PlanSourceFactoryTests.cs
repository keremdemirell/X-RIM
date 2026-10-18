using System;
using System.Collections.Generic;
using NUnit.Framework;
using XRim.App;
using XRim.Bots;
using XRim.Core;
using XRim.Networking;
using XRim.Rules.Match;
using XRim.Rules.Settings;

namespace XRim.Tests.EditMode.App
{
    /// <summary>Who plans each side in each mode (D25: dev opponents only).</summary>
    public sealed class PlanSourceFactoryTests
    {
        private RulesSettings _settings;
        private MatchSetup _setup;
        private List<Side> _humansAsked;

        [SetUp]
        public void SetUp()
        {
            _settings = GddStartingValues.CreateRulesSettings();
            _setup = TestData.CreateMatchSetup();
            _humansAsked = new List<Side>();
        }

        private IReadOnlyList<IPlanSource> Create(MatchMode mode, Side humanSide = Side.Left) =>
            PlanSourceFactory.Create(mode, _setup, _settings, Human, new XorShiftRandom(TestData.Seed), humanSide);

        private IPlanSource Human(Side side)
        {
            _humansAsked.Add(side);
            return new HumanStandIn(side);
        }

        [TestCase(Side.Left)]
        [TestCase(Side.Right)]
        public void HumanVsBot_PutsTheHumanOnTheirSide_AndTheBotOnTheOther(Side humanSide)
        {
            IReadOnlyList<IPlanSource> sources = Create(MatchMode.HumanVsBot, humanSide);

            Assert.That(_humansAsked, Is.EqualTo(new[] { humanSide }));
            Assert.That(sources.Count, Is.EqualTo(2));
            Assert.That(sources[0], Is.InstanceOf<HumanStandIn>());
            Assert.That(sources[0].Side, Is.EqualTo(humanSide));
            Assert.That(sources[1], Is.InstanceOf<BotPlanSource>());
            Assert.That(sources[1].Side, Is.EqualTo(humanSide.Opponent()));
        }

        [Test]
        public void HotSeat_HasAHumanOnEachSide()
        {
            IReadOnlyList<IPlanSource> sources = Create(MatchMode.HotSeat);

            Assert.That(_humansAsked, Is.EqualTo(new[] { Side.Left, Side.Right }));
            Assert.That(sources[0].Side, Is.EqualTo(Side.Left));
            Assert.That(sources[1].Side, Is.EqualTo(Side.Right));
        }

        [Test]
        public void BotVsBot_AsksForNoHuman()
        {
            IReadOnlyList<IPlanSource> sources = Create(MatchMode.BotVsBot);

            Assert.That(_humansAsked, Is.Empty);
            Assert.That(sources[0], Is.InstanceOf<BotPlanSource>());
            Assert.That(sources[1], Is.InstanceOf<BotPlanSource>());
            Assert.That(sources[0].Side, Is.EqualTo(Side.Left));
            Assert.That(sources[1].Side, Is.EqualTo(Side.Right));
        }

        [Test]
        public void ByDefault_EveryBotIsTheHeuristicOne()
        {
            Assert.That(((BotPlanSource)Create(MatchMode.HumanVsBot)[1]).Brain, Is.InstanceOf<HeuristicBotBrain>());
            IReadOnlyList<IPlanSource> watched = Create(MatchMode.BotVsBot);
            Assert.That(((BotPlanSource)watched[0]).Brain, Is.InstanceOf<HeuristicBotBrain>());
            Assert.That(((BotPlanSource)watched[1]).Brain, Is.InstanceOf<HeuristicBotBrain>());
        }

        [Test]
        public void TheBotsSettings_PickEachBotsBrain()
        {
            var bots = new BotSettings { VersusBrain = BotBrainKind.Random, WatchLeftBrain = BotBrainKind.Heuristic, WatchRightBrain = BotBrainKind.Random };

            IPlanSource versus = PlanSourceFactory.Create(MatchMode.HumanVsBot, _setup, _settings, Human, new XorShiftRandom(TestData.Seed), Side.Left, bots)[1];
            IReadOnlyList<IPlanSource> watched = PlanSourceFactory.Create(MatchMode.BotVsBot, _setup, _settings, Human, new XorShiftRandom(TestData.Seed),
                Side.Left, bots);

            Assert.That(((BotPlanSource)versus).Brain, Is.InstanceOf<RandomBotBrain>());
            Assert.That(((BotPlanSource)watched[0]).Brain, Is.InstanceOf<HeuristicBotBrain>());
            Assert.That(((BotPlanSource)watched[1]).Brain, Is.InstanceOf<RandomBotBrain>());
        }

        [Test]
        public void TheSandbox_GetsItsSourcesFromTheDebugTools()
        {
            Assert.Throws<ArgumentException>(() => Create(MatchMode.Sandbox));
        }

        private sealed class HumanStandIn : IPlanSource
        {
            public Side Side { get; }

            public HumanStandIn(Side side)
            {
                Side = side;
            }

            public void OnPlanningStarted(PlanningWindow window, IPlanningCommandSink sink)
            {
            }

            public void OnPlanningEnded()
            {
            }
        }
    }
}
