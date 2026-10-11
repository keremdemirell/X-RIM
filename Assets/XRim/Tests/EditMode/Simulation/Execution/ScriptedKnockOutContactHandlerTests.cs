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
using XRim.Simulation;
using XRim.Simulation.Execution;
using XRim.Simulation.Physics;
using XRim.Simulation.Recording;

namespace XRim.Tests.EditMode.Simulation.Execution
{
    /// <summary>
    /// The force-double-KO cheat (Session 12) on the fake world: queued dummies die at the start of the next turn through the real death
    /// path, so the rules see a real double KO (§3) or, in sudden death, two deaths at the same instant (§14).
    /// </summary>
    public sealed class ScriptedKnockOutContactHandlerTests
    {
        private RulesSettings _rules;
        private SimulationSettings _simulation;
        private FakePhysicsWorld _world;
        private ScriptedKnockOutContactHandler _cheat;

        [SetUp]
        public void SetUp()
        {
            _rules = GddStartingValues.CreateRulesSettings();
            _simulation = new SimulationSettings();
            _world = new FakePhysicsWorld();
            _cheat = new ScriptedKnockOutContactHandler(new HitContactHandler());
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
            var simulator = new TurnSimulator(_world, new RulePolicies(), new TurnSimulatorOptions { Contacts = _cheat });
            return simulator.Simulate(new TurnInput(new BoardSnapshot(state, pose), plans, _rules, _simulation));
        }

        [Test]
        public void AQueuedDoubleKo_KillsBothAtTheTurnsStart_ThroughTheRealDeathPath()
        {
            _cheat.QueueDoubleKnockOut();
            Assert.That(_cheat.Queued, Is.EquivalentTo(new[] { Side.Left, Side.Right }));

            TurnResult result = Run();

            Assert.That(result.FinalBoard.State.Fighters.Left.IsDead && result.FinalBoard.State.Fighters.Right.IsDead, Is.True);
            Assert.That(result.Timeline.Events.OfType<FighterDiedEvent>().Select(e => (e.Side, e.Time)),
                Is.EquivalentTo(new[] { (Side.Left, SimTime.Zero), (Side.Right, SimTime.Zero) }));
            Assert.That(_world.Splits, Is.EquivalentTo(new[] { Side.Left, Side.Right }), "limp, the torso split");
            Assert.That(_world.Drops, Is.EquivalentTo(new[] { Side.Left, Side.Right }), "both weapons drop");
            Assert.That(result.Report.DeathTime.Left, Is.EqualTo(SimTime.Zero));
            Assert.That(result.Report.DeathTime.Right, Is.EqualTo(SimTime.Zero));
        }

        [Test]
        public void TheEndConditions_SeeARealDoubleKo()
        {
            _cheat.QueueDoubleKnockOut();
            TurnResult result = Run();

            EndCheckResult end = new EndConditionEvaluator().Evaluate(result.Report, _rules, new RulePolicies(), TestData.Seed);

            Assert.That(end.Resolution, Is.EqualTo(TurnResolution.EnterSuddenDeath));
        }

        [Test]
        public void EachQueuedKnockOut_IsUsedOnce()
        {
            _cheat.QueueKnockOut(Side.Right);
            Run();

            TurnResult next = Run();

            Assert.That(_cheat.Queued, Is.Empty);
            Assert.That(next.Timeline.Events.OfType<FighterDiedEvent>(), Is.Empty);
            Assert.That(next.FinalBoard.State.Fighters.Right.IsDead, Is.False);
        }
    }
}
