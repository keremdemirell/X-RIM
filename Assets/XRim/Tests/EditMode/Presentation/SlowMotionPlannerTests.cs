using System.Linq;
using NUnit.Framework;
using XRim.Core;
using XRim.Presentation.Feel;
using XRim.Rules;
using XRim.Rules.Events;
using XRim.Rules.Match;
using XRim.Rules.Settings;
using XRim.Simulation.Physics;
using XRim.Simulation.Recording;

namespace XRim.Tests.EditMode.Presentation
{
    /// <summary>
    /// Which moments slow a turn down (Session 13): a sever, a death, an interrupt counter-hit and a sudden-death finish, each starting its
    /// share of anticipation before the moment. An ordinary hit does not.
    /// </summary>
    public sealed class SlowMotionPlannerTests
    {
        private const float Tolerance = 1e-4f;

        private static SlowMotionSettings Settings() => new SlowMotionSettings
        {
            Sever = new SlowMotionMoment(0.25f, 1f, 0.3f),
            KnockOut = new SlowMotionMoment(0.2f, 1.5f, 0.2f),
            CounterHit = new SlowMotionMoment(0.4f, 0.5f, 0.5f),
            SuddenDeathFinish = new SlowMotionMoment(0.1f, 2f, 0.4f),
            RampTurnSeconds = 0.03f,
        };

        [Test]
        public void ASever_IsSlowedFromAShareBeforeIt()
        {
            SimTime cut = SimTime.FromSeconds(0.4);
            SlowMotionPlan plan = SlowMotionPlanner.Plan(Turn(new LimbSeveredEvent(cut, Side.Right, BodyPart.LeftArm)), Settings());

            SlowMotionWindow window = plan.Windows.Single();
            Assert.That(window.Moment, Is.EqualTo(cut));
            Assert.That(window.Speed, Is.EqualTo(0.25f));
            // 1 s on screen at a quarter speed covers 0.25 s of the turn, 30% of it before the cut.
            Assert.That(window.Start.Seconds, Is.EqualTo(0.4 - 0.075).Within(Tolerance));
            Assert.That(window.End.Seconds, Is.EqualTo(0.4 + 0.175).Within(Tolerance));
            Assert.That(window.RampSeconds, Is.EqualTo(0.03f));
        }

        [Test]
        public void ADeathAndACounterHit_EachGetTheirOwnStretch()
        {
            SimTime time = SimTime.FromSeconds(0.3);
            SlowMotionPlan plan = SlowMotionPlanner.Plan(Turn(new AttackInterruptedEvent(time, Side.Right), new FighterDiedEvent(time, Side.Right)),
                Settings());

            Assert.That(plan.Windows.Select(window => window.Speed), Is.EquivalentTo(new[] { 0.4f, 0.2f }));
            Assert.That(plan.SpeedAt(time), Is.EqualTo(0.2f), "the slower one wins where they overlap");
        }

        [Test]
        public void AnOrdinaryHit_IsNotSlowed()
        {
            SlowMotionPlan plan = SlowMotionPlanner.Plan(Turn(PlayedTurn.Hit(Side.Right, 0.3, 10f)), Settings());

            Assert.That(plan.Windows, Is.Empty);
        }

        [Test]
        public void AMomentSwitchedOff_IsNotSlowed()
        {
            SlowMotionSettings settings = Settings();
            settings.Sever = new SlowMotionMoment(0.25f, 0f, 0.3f);

            SlowMotionPlan plan = SlowMotionPlanner.Plan(Turn(new LimbSeveredEvent(SimTime.FromSeconds(0.2), Side.Left, BodyPart.LeftLeg)), settings);

            Assert.That(plan.Windows, Is.Empty);
        }

        [Test]
        public void ASuddenDeathTurn_IsSlowedAroundItsFirstDecisiveMoment()
        {
            SimTime leftHit = SimTime.FromSeconds(0.32);
            SimTime rightHit = SimTime.FromSeconds(0.35);
            TurnResult turn = Turn(suddenDeath: true, firstHits: new PerSide<SimTime?>(leftHit, rightHit));

            Assert.That(SlowMotionPlanner.TryFindSuddenDeathFinish(turn, out SimTime finish), Is.True);
            Assert.That(finish, Is.EqualTo(leftHit), "the earlier hit decides (§14)");
            SlowMotionWindow window = SlowMotionPlanner.Plan(turn, Settings()).Windows.Single();
            Assert.That(window.Moment, Is.EqualTo(leftHit));
            Assert.That(window.Speed, Is.EqualTo(0.1f));
        }

        [Test]
        public void ANormalTurn_HasNoSuddenDeathFinish()
        {
            TurnResult turn = Turn(suddenDeath: false, firstHits: new PerSide<SimTime?>(SimTime.FromSeconds(0.3), null));

            Assert.That(SlowMotionPlanner.TryFindSuddenDeathFinish(turn, out _), Is.False);
        }

        private static TurnResult Turn(params MatchEvent[] events) => Turn(false, new PerSide<SimTime?>(null, null), events);

        private static TurnResult Turn(bool suddenDeath, PerSide<SimTime?> firstHits, params MatchEvent[] events)
        {
            RulesSettings settings = GddStartingValues.CreateRulesSettings();
            BoardSnapshot board = TestData.CreateInitialBoard(settings);
            board.State.IsSuddenDeath = suddenDeath;
            var recorder = new TimelineRecorder();
            recorder.RecordFrame(0, SimTime.Zero, new PoseSnapshot());
            recorder.RecordFrame(1, SimTime.FromSeconds(1.0), new PoseSnapshot());
            foreach (MatchEvent matchEvent in events)
            {
                recorder.RecordEvent(matchEvent);
            }

            return new TurnResult(0, recorder.Build(), board, new ExecutionReport(board.State, firstHits, false));
        }
    }
}
