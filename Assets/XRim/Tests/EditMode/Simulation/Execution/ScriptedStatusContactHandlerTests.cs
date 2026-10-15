using System.Linq;
using NUnit.Framework;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Arena;
using XRim.Rules.Events;
using XRim.Rules.Match;
using XRim.Rules.Paths;
using XRim.Rules.Planning;
using XRim.Rules.Settings;
using XRim.Rules.Status;
using XRim.Simulation;
using XRim.Simulation.Execution;
using XRim.Simulation.Physics;
using XRim.Simulation.Recording;

namespace XRim.Tests.EditMode.Simulation.Execution
{
    /// <summary>
    /// The force-stun cheat (Session 13) on the fake world: a queued stun or stagger lands at the start of the next turn through the
    /// match's own status factory and a real <see cref="StatusAppliedEvent"/>, so it shapes the following planning exactly as a head stun
    /// or a lost crush does. A dummy knocked out first gets none.
    /// </summary>
    public sealed class ScriptedStatusContactHandlerTests
    {
        private RulesSettings _rules;
        private SimulationSettings _simulation;
        private FakePhysicsWorld _world;
        private CountingStatusFactory _factory;
        private ScriptedStatusContactHandler _cheat;
        private ScriptedKnockOutContactHandler _knockOut;

        [SetUp]
        public void SetUp()
        {
            _rules = GddStartingValues.CreateRulesSettings();
            _simulation = new SimulationSettings();
            _world = new FakePhysicsWorld();
            _factory = new CountingStatusFactory();
            _cheat = new ScriptedStatusContactHandler(new HitContactHandler());
            _knockOut = new ScriptedKnockOutContactHandler(_cheat);
        }

        [TearDown]
        public void TearDown() => _world.Dispose();

        private TurnResult Run()
        {
            var state = new MatchState(
                PerSide<FighterState>.Create(_ => new FighterState(Handedness.Right, _rules.Damage.MaxHp, WeaponIds.Rapier)),
                PerSide<ElectricWallState>.Create(_ => new ElectricWallState()));
            var pose = new PoseSnapshot();
            pose.Left.Set(BodyPart.Torso, new BodyPose(new Vec2(-350f, 180f), 0f));
            pose.Right.Set(BodyPart.Torso, new BodyPose(new Vec2(350f, 180f), 0f));
            pose.Left.HeldItem = new BodyPose(new Vec2(-350f, 280f), 0f);
            pose.Right.HeldItem = new BodyPose(new Vec2(350f, 280f), 180f);
            var plans = PerSide<TurnPlan>.Create(_ => new TurnPlan(WeaponIds.Rapier, BodyMove.None, WeaponPath.Empty, default, true));
            var policies = new RulePolicies { StatusEffects = _factory };
            var simulator = new TurnSimulator(_world, policies, new TurnSimulatorOptions { Contacts = _knockOut });
            return simulator.Simulate(new TurnInput(new BoardSnapshot(state, pose), plans, _rules, _simulation));
        }

        [Test]
        public void AQueuedStun_LandsAtTheTurnsStart_ThroughTheMatchsStatusFactory()
        {
            _cheat.QueueStatus(Side.Right, StatusKind.Stunned);

            TurnResult result = Run();

            Assert.That(result.FinalBoard.State.Fighters.Right.Statuses.Select(s => s.Kind), Is.EqualTo(new[] { StatusKind.Stunned }));
            Assert.That(result.FinalBoard.State.Fighters.Left.Statuses, Is.Empty);
            Assert.That(result.Timeline.Events.OfType<StatusAppliedEvent>().Select(e => (e.Side, e.Kind, e.Time)),
                Is.EqualTo(new[] { (Side.Right, StatusKind.Stunned, SimTime.Zero) }));
            Assert.That(_factory.Created, Is.EqualTo(1), "made by the match's factory, as a head hit's stun is");
        }

        [Test]
        public void AStagger_CanBeQueuedForEitherSide()
        {
            _cheat.QueueStatus(Side.Left, StatusKind.Staggered);
            _cheat.QueueStatus(Side.Left, StatusKind.Staggered);
            Assert.That(_cheat.Queued.Count, Is.EqualTo(1), "queued once");

            TurnResult result = Run();

            Assert.That(result.FinalBoard.State.Fighters.Left.Statuses.Select(s => s.Kind), Is.EqualTo(new[] { StatusKind.Staggered }));
        }

        [Test]
        public void TheNextPlanning_IsShapedAsByARealStun()
        {
            _cheat.QueueStatus(Side.Right, StatusKind.Stunned);
            IStatusEffect cheated = Run().FinalBoard.State.Fighters.Right.Statuses.Single();
            IStatusEffect real = new SettingsStatusEffectFactory().Create(StatusKind.Stunned, _rules.Damage);

            PlanningConstraints afterCheat = PlanningConstraints.CreateDefault(_rules.Match);
            PlanningConstraints afterRealStun = PlanningConstraints.CreateDefault(_rules.Match);
            cheated.ApplyToNextTurn(afterCheat);
            real.ApplyToNextTurn(afterRealStun);

            Assert.That(cheated.RemainingTurns, Is.EqualTo(real.RemainingTurns));
            foreach (BodyMove move in new[] { BodyMove.Lunge, BodyMove.Crouch, BodyMove.Jump, BodyMove.StepBack })
                Assert.That(afterCheat.IsBodyMoveAllowed(move), Is.EqualTo(afterRealStun.IsBodyMoveAllowed(move)), move.ToString());
            Assert.That(afterCheat.PlanningDurationSeconds, Is.EqualTo(afterRealStun.PlanningDurationSeconds));
            Assert.That(afterCheat.InkLengthMultiplier, Is.EqualTo(afterRealStun.InkLengthMultiplier));
        }

        [Test]
        public void ADummyKnockedOutFirst_GetsNoStatus()
        {
            _knockOut.QueueKnockOut(Side.Right);
            _cheat.QueueStatus(Side.Right, StatusKind.Stunned);

            TurnResult result = Run();

            Assert.That(result.FinalBoard.State.Fighters.Right.IsDead, Is.True);
            Assert.That(result.FinalBoard.State.Fighters.Right.Statuses, Is.Empty);
            Assert.That(result.Timeline.Events.OfType<StatusAppliedEvent>(), Is.Empty);
        }

        [Test]
        public void EachQueuedStatus_IsUsedOnce()
        {
            _cheat.QueueStatus(Side.Right, StatusKind.Stunned);
            Run();

            TurnResult next = Run();

            Assert.That(_cheat.Queued, Is.Empty);
            Assert.That(next.Timeline.Events.OfType<StatusAppliedEvent>(), Is.Empty);
        }

        private sealed class CountingStatusFactory : IStatusEffectFactory
        {
            private readonly SettingsStatusEffectFactory _inner = new SettingsStatusEffectFactory();

            public int Created { get; private set; }

            public IStatusEffect Create(StatusKind kind, DamageSettings settings)
            {
                Created++;
                return _inner.Create(kind, settings);
            }
        }
    }
}
