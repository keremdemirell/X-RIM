using System.Collections.Generic;
using NUnit.Framework;
using XRim.Core;
using XRim.Networking;
using XRim.Rules;
using XRim.Rules.Match;
using XRim.Rules.Planning;
using XRim.Rules.Settings;
using XRim.Simulation;

namespace XRim.Tests.EditMode.Networking
{
    public sealed class NetworkingSanityTests
    {
        [Test]
        public void LocalTurnAuthority_StartsInMatchSetup()
        {
            var authority = new LocalTurnAuthority(
                TestData.CreateMatchSetup(), GddStartingValues.CreateRulesSettings(), new SimulationSettings(),
                new RulePolicies(), new StubTurnSimulator(), new ManualClock());

            Assert.That(authority.Phase, Is.EqualTo(MatchPhase.MatchSetup));
        }

        [Test]
        public void PlanSourceBinder_RoutesEachSourceToItsOwnSide()
        {
            RulesSettings settings = GddStartingValues.CreateRulesSettings();
            var authority = new FakeTurnAuthority();
            var right = new RecordingPlanSource(Side.Right);

            using (new PlanSourceBinder(authority, new IPlanSource[] { right }))
            {
                authority.RaisePlanningStarted(CreateWindow(settings));
                right.Sink.Send(new SetReadyCommand(true));
                authority.RaisePlanningLocked(0);
            }

            Assert.That(authority.Received.Count, Is.EqualTo(1));
            Assert.That(authority.Received[0].Side, Is.EqualTo(Side.Right));
            Assert.That(right.Ended, Is.True);
        }

        internal static PlanningWindow CreateWindow(RulesSettings settings) => new PlanningWindow(
            0, settings.Match.PlanningDurationSeconds,
            PerSide<PlanningConstraints>.Create(_ => PlanningConstraints.CreateDefault(settings.Match)),
            false, TestData.CreateInitialBoard(settings));

        private sealed class RecordingPlanSource : IPlanSource
        {
            public Side Side { get; }
            public IPlanningCommandSink Sink { get; private set; }
            public bool Ended { get; private set; }

            public RecordingPlanSource(Side side)
            {
                Side = side;
            }

            public void OnPlanningStarted(PlanningWindow window, IPlanningCommandSink sink) => Sink = sink;

            public void OnPlanningEnded() => Ended = true;
        }
    }
}
