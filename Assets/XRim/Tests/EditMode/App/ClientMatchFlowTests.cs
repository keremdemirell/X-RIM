using System.Collections.Generic;
using NUnit.Framework;
using XRim.App;
using XRim.Core;
using XRim.Networking;
using XRim.Rules;
using XRim.Rules.Match;
using XRim.Rules.Settings;
using XRim.Simulation;
using XRim.Tests.EditMode.Networking;

namespace XRim.Tests.EditMode.App
{
    /// <summary>
    /// The client flow against the real authority, rules and bots, with a scripted simulator standing in for physics (GDD §3):
    /// Planning → WaitingForAuthority → Playback → … → MatchOver, the next planning timer starting only once playback has finished,
    /// and the end of the match held until its last turn has played.
    /// </summary>
    public sealed class ClientMatchFlowTests
    {
        private const double StartSeconds = 10.0;
        private const double PlaybackSeconds = 3.0;
        private const int TickLimit = 200;

        private ManualClock _clock;
        private RulesSettings _settings;
        private LocalTurnAuthority _authority;
        private ClientMatchFlow _flow;
        private PlanSourceBinder _binder;
        private List<ClientPhase> _phases;
        private List<PlanningWindow> _windows;
        private List<int> _resolved;

        [SetUp]
        public void SetUp()
        {
            _clock = new ManualClock(StartSeconds);
            _settings = GddStartingValues.CreateRulesSettings();
            _phases = new List<ClientPhase>();
            _windows = new List<PlanningWindow>();
            _resolved = new List<int>();
        }

        [TearDown]
        public void TearDown()
        {
            _binder?.Dispose();
            _flow?.Dispose();
        }

        /// <summary>Two random bots through the real authority, holding each next planning phase for the client's report.</summary>
        private void Compose(ScriptedTurnSimulator simulator)
        {
            MatchSetup setup = TestData.CreateMatchSetup();
            var options = new LocalTurnAuthorityOptions { PlaybackHold = new PlaybackHoldSettings { Mode = PlaybackHoldMode.ClientReport } };
            _authority = new LocalTurnAuthority(setup, _settings, new SimulationSettings(), new RulePolicies(), simulator, _clock, options);
            _authority.PlanningStarted += _windows.Add;
            _authority.TurnResolved += result => _resolved.Add(result.TurnIndex);

            // As in the match scene: the flow follows the authority before the plan sources are bound.
            _flow = new ClientMatchFlow(_authority);
            _flow.PhaseChanged += _phases.Add;
            IReadOnlyList<IPlanSource> bots = PlanSourceFactory.Create(MatchMode.BotVsBot, setup, _settings, NoHuman, new XorShiftRandom(TestData.Seed));
            _binder = new PlanSourceBinder(_authority, bots);
        }

        private static IPlanSource NoHuman(Side side)
        {
            Assert.Fail("Bot vs bot asks for no human.");
            return null;
        }

        /// <summary>What the match scene does each frame, with playback taking <see cref="PlaybackSeconds"/> of clock time.</summary>
        private void PlayUntilOver()
        {
            _authority.Start();
            for (int tick = 0; tick < TickLimit && _flow.Phase != ClientPhase.MatchOver; tick++)
            {
                _authority.Tick();
                if (_flow.Phase != ClientPhase.Playback) continue;
                _clock.Advance(PlaybackSeconds);
                _flow.OnPlaybackFinished();
            }
        }

        [Test]
        public void EachTurn_GoesPlanningWaitingPlaybackWaiting_ThenTheNextPlanning()
        {
            Compose(new ScriptedTurnSimulator((turn, state) => state.Fighters[Side.Right].Hp -= 40f));

            PlayUntilOver();

            var expected = new List<ClientPhase>();
            for (int turn = 0; turn < 3; turn++)
            {
                expected.Add(ClientPhase.Planning);
                expected.Add(ClientPhase.WaitingForAuthority); // plans locked
                expected.Add(ClientPhase.Playback);
                if (turn < 2) expected.Add(ClientPhase.WaitingForAuthority); // playback done, the authority opens the next turn
            }

            expected.Add(ClientPhase.MatchOver);
            Assert.That(_phases, Is.EqualTo(expected));
            Assert.That(_resolved, Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.That(_flow.Outcome.Winner, Is.EqualTo(Side.Left));
            Assert.That(_flow.Outcome.Reason, Is.EqualTo(MatchEndReason.KnockOut));
            Assert.That(_flow.Outcome.TurnCount, Is.EqualTo(3), "100 HP at 40 a turn");
        }

        [Test]
        public void TheNextPlanningTimer_StartsOnlyAfterPlaybackHasFinished()
        {
            Compose(new ScriptedTurnSimulator());
            _authority.Start();
            _authority.Tick(); // the bots were Ready at once: turn 1 resolves
            Assert.That(_flow.Phase, Is.EqualTo(ClientPhase.Playback));

            _clock.Advance(PlaybackSeconds);
            for (int tick = 0; tick < 10; tick++) _authority.Tick();
            Assert.That(_windows, Has.Count.EqualTo(1), "no planning while the replay plays");

            double finished = _clock.NowSeconds;
            _flow.OnPlaybackFinished();
            Assert.That(_flow.Phase, Is.EqualTo(ClientPhase.WaitingForAuthority));
            _authority.Tick();

            Assert.That(_windows, Has.Count.EqualTo(2));
            Assert.That(_phases.GetRange(_phases.Count - 2, 2), Is.EqualTo(new[] { ClientPhase.Planning, ClientPhase.WaitingForAuthority }),
                "planning opened (and the bots were Ready at once)");
            Assert.That(_flow.CurrentWindow, Is.SameAs(_windows[1]));
            Assert.That(_windows[1].DeadlineSeconds, Is.EqualTo(finished + _settings.Match.PlanningDurationSeconds),
                "the full planning time, counted from the end of playback");
        }

        [Test]
        public void TheEndOfTheMatch_WaitsForTheLastPlayback()
        {
            Compose(new ScriptedTurnSimulator((turn, state) => state.Fighters[Side.Left].Hp = 0f));
            _authority.Start();
            _authority.Tick();

            Assert.That(_authority.Outcome, Is.Not.Null, "the authority knows");
            Assert.That(_flow.Phase, Is.EqualTo(ClientPhase.Playback), "the client shows the killing turn first");
            Assert.That(_flow.Outcome, Is.Null);

            _flow.OnPlaybackFinished();

            Assert.That(_flow.Phase, Is.EqualTo(ClientPhase.MatchOver));
            Assert.That(_flow.Outcome.Winner, Is.EqualTo(Side.Right));
            for (int tick = 0; tick < 10; tick++) _authority.Tick();
            Assert.That(_windows, Has.Count.EqualTo(1), "no planning after the KO");
        }

        [Test]
        public void FinishingPlayback_ReportsThatTurnToTheAuthority_Once()
        {
            RulesSettings settings = GddStartingValues.CreateRulesSettings();
            var authority = new FakeTurnAuthority();
            using (var flow = new ClientMatchFlow(authority))
            {
                authority.RaisePlanningStarted(NetworkingSanityTests.CreateWindow(settings));
                flow.OnPlaybackFinished(); // a debug replay watched while planning: not a turn's playback
                authority.RaisePlanningLocked(0);
                authority.RaiseTurnResolved(AppSanityTests.CreateResult(settings, 4));

                flow.OnPlaybackFinished();
                flow.OnPlaybackFinished();

                Assert.That(authority.PlaybackFinishedTurns, Is.EqualTo(new[] { 4 }));
            }
        }
    }
}
