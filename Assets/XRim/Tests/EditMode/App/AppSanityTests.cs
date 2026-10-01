using NUnit.Framework;
using XRim.App;
using XRim.Core;
using XRim.Networking;
using XRim.Rules;
using XRim.Rules.Match;
using XRim.Rules.Planning;
using XRim.Rules.Settings;
using XRim.Simulation;
using XRim.Simulation.Execution;
using XRim.Simulation.Recording;
using XRim.Tests.EditMode.Networking;

namespace XRim.Tests.EditMode.App
{
    public sealed class AppSanityTests
    {
        [Test]
        public void ClientFlow_ShowsTheResultBeforeTheNextPlanningPhase()
        {
            RulesSettings settings = GddStartingValues.CreateRulesSettings();
            var authority = new FakeTurnAuthority();
            using (var flow = new ClientMatchFlow(authority))
            {
                authority.RaisePlanningStarted(NetworkingSanityTests.CreateWindow(settings));
                Assert.That(flow.Phase, Is.EqualTo(ClientPhase.Planning));

                authority.RaisePlanningLocked(0);
                Assert.That(flow.Phase, Is.EqualTo(ClientPhase.WaitingForAuthority));

                authority.RaiseTurnResolved(CreateResult(settings));
                Assert.That(flow.Phase, Is.EqualTo(ClientPhase.Playback));

                PlanningWindow next = NetworkingSanityTests.CreateWindow(settings);
                authority.RaisePlanningStarted(next);
                Assert.That(flow.Phase, Is.EqualTo(ClientPhase.Playback), "Planning waits until playback ends");

                flow.OnPlaybackFinished();
                Assert.That(flow.Phase, Is.EqualTo(ClientPhase.Planning));
                Assert.That(flow.CurrentWindow, Is.SameAs(next));
            }
        }

        [Test]
        public void RecordingSimulator_RemembersTheLastTurnsInput()
        {
            RulesSettings settings = GddStartingValues.CreateRulesSettings();
            var simulator = new RecordingTurnSimulator(new StubTurnSimulator());
            var input = new TurnInput(TestData.CreateInitialBoard(settings), PerSide<TurnPlan>.Create(_ => TurnPlan.Empty(WeaponIds.Rapier)),
                settings, new SimulationSettings());

            Assert.That(simulator.LastInput, Is.Null);
            simulator.Simulate(input);

            Assert.That(simulator.LastInput, Is.SameAs(input));
        }

        private static TurnResult CreateResult(RulesSettings settings)
        {
            BoardSnapshot board = TestData.CreateInitialBoard(settings);
            var report = new ExecutionReport(board.State, new PerSide<SimTime?>(null, null), false);
            return new TurnResult(0, new TimelineRecorder().Build(), board, report);
        }
    }
}
