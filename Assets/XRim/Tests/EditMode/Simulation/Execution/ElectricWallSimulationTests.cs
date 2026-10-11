using System.Collections.Generic;
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
    /// The electric wall inside the turn loop, on the fake world (GDD §13; the designer's geometry P1, 2026-10-11). The fake world's torso
    /// follows its root target exactly and nothing collides, so touches are scripted. The left pelvis starts at x = −350, standing, and its
    /// step back ends 100 further back; the right dummy stands at 350. Walls are locked the way the match does it, before the turn.
    /// </summary>
    public sealed class ElectricWallSimulationTests
    {
        private const float LeftX = -350f;
        private const float RightX = 350f;
        private const float StepBackUnits = 100f;
        private const float TorsoHalfWidth = 30f;
        private const int TouchStep = 6;
        private const float Tolerance = 1e-3f;

        private readonly ElectricWallRules _wallRules = new ElectricWallRules();
        private RulesSettings _rules;
        private SimulationSettings _simulation;
        private FakePhysicsWorld _world;
        private SimClock _clock;
        private PoseSnapshot _pose;
        private MatchState _state;

        [SetUp]
        public void SetUp()
        {
            _rules = GddStartingValues.CreateRulesSettings();
            _simulation = new SimulationSettings();
            _world = new FakePhysicsWorld();
            _clock = new SimClock(_simulation.StepRateHz);
            _state = new MatchState(
                PerSide<FighterState>.Create(_ => new FighterState(Handedness.Right, _rules.Damage.MaxHp, WeaponIds.Rapier)),
                PerSide<ElectricWallState>.Create(_ => new ElectricWallState()));
            float standing = _simulation.Ragdoll.LegLengthUnits;
            _pose = new PoseSnapshot();
            _pose.Left.Set(BodyPart.Torso, new BodyPose(new Vec2(LeftX, standing), 0f));
            _pose.Right.Set(BodyPart.Torso, new BodyPose(new Vec2(RightX, standing), 0f));
            _pose.Left.HeldItem = new BodyPose(new Vec2(LeftX, standing + 100f), 0f);
            _pose.Right.HeldItem = new BodyPose(new Vec2(RightX, standing + 100f), 180f);
        }

        [TearDown]
        public void TearDown() => _world.Dispose();

        private ElectricWallSettings Wall => _rules.ElectricWall;

        private float WarningX => LeftX - Wall.SpawnOffsetBehindUnits;

        /// <summary>The board after turn N: the left warning stands 60 behind the pelvis.</summary>
        private void WarningStandsBehindLeft()
        {
            _wallRules.OnPlansLocked(_state.Walls.Left, BodyMove.StepBack, false, Wall);
            _wallRules.RaiseWarning(SimTime.Zero, Side.Left, _state.Walls.Left, LeftX, new ArenaEdges(-1000f, 1000f, true), Wall);
        }

        private TurnResult Run(BodyMove left, BodyMove right = BodyMove.None, MatchState state = null, PoseSnapshot pose = null)
        {
            state = state ?? _state;
            _wallRules.OnPlansLocked(state.Walls.Left, left, state.IsSuddenDeath, Wall);
            _wallRules.OnPlansLocked(state.Walls.Right, right, state.IsSuddenDeath, Wall);
            var plans = new PerSide<TurnPlan>(Plan(left), Plan(right));
            return new TurnSimulator(_world, new RulePolicies()).Simulate(new TurnInput(new BoardSnapshot(state, pose ?? _pose), plans, _rules, _simulation));
        }

        private static TurnPlan Plan(BodyMove move) => new TurnPlan(WeaponIds.Rapier, move, WeaponPath.Empty, default, true);

        /// <summary>The owner's body touches its own wall during a step (the engine reports it after that step).</summary>
        private void ScheduleTouch(Side owner, BodyPart part, int step, float wallX) =>
            _world.ScheduleContacts(step, new ContactFacts(new BodyTag(owner, BodyRole.ElectricWall, BodyPart.Torso),
                new BodyTag(owner, BodyRole.BodyPart, part), new Vec2(wallX, 20f), new Vec2(owner.FacingSign(), 0f), Vec2.Zero));

        private static List<ElectricWallEvent> WallEvents(TurnResult result, ElectricWallPhase phase) =>
            result.Timeline.Events.OfType<ElectricWallEvent>().Where(e => e.Phase == phase).ToList();

        private float FinalTorsoX(TurnResult result, Side side) => result.FinalBoard.Pose.Get(side).Get(BodyPart.Torso).PositionUnits.X;

        // --- Turn N: the warning (§13, P1) ----------------------------------------------------------

        [Test]
        public void AFirstStepBack_RaisesTheWarning_WhenTheStepBackEnds_SixtyBehindThePelvis()
        {
            BodyMoveStats stepBack = _rules.FindBodyMove(BodyMove.StepBack);

            TurnResult result = Run(BodyMove.StepBack);

            ElectricWallEvent raised = WallEvents(result, ElectricWallPhase.Warning).Single();
            float expectedX = LeftX - StepBackUnits - Wall.SpawnOffsetBehindUnits;
            Assert.That(raised.Side, Is.EqualTo(Side.Left));
            Assert.That(raised.HpDamage, Is.EqualTo(0f));
            Assert.That(raised.PositionUnits, Is.EqualTo(expectedX).Within(Tolerance));
            Assert.That(raised.Time.Seconds, Is.EqualTo(stepBack.DurationSeconds).Within(_clock.StepSeconds), "when the step back ends");
            Assert.That(_world.WallCommands.Single().XUnits, Is.EqualTo(expectedX).Within(Tolerance), "it springs up in the world");

            ElectricWallState wall = result.FinalBoard.State.Walls.Left;
            Assert.That((wall.Phase, wall.IsUp), Is.EqualTo((ElectricWallPhase.Warning, true)));
            Assert.That(wall.PositionUnits, Is.EqualTo(expectedX).Within(Tolerance));
            Assert.That(result.FinalBoard.State.Fighters.Left.Hp, Is.EqualTo(_rules.Damage.MaxHp), "a warning never hurts");
        }

        [Test]
        public void TheWarning_IsDrawnFromTheFrameItSpringsUp_AndStaysOnTheBoard()
        {
            TurnResult result = Run(BodyMove.StepBack);

            SimTime raisedAt = WallEvents(result, ElectricWallPhase.Warning).Single().Time;
            foreach (TimelineFrame frame in result.Timeline.Frames)
            {
                Assert.That(frame.Pose.LeftWall.IsUp, Is.EqualTo(frame.Time >= raisedAt), $"frame at {frame.Time}");
                Assert.That(frame.Pose.RightWall.IsUp, Is.False);
            }

            ElectricWallPose board = result.FinalBoard.Pose.LeftWall;
            Assert.That((board.IsUp, board.Phase), Is.EqualTo((true, ElectricWallPhase.Warning)));
        }

        [TestCase(BodyMove.None)]
        [TestCase(BodyMove.Crouch)]
        [TestCase(BodyMove.Lunge)]
        [TestCase(BodyMove.Jump)]
        public void NoWall_ForAnyOtherMove(BodyMove move)
        {
            TurnResult result = Run(move);

            Assert.That(result.Timeline.Events.OfType<ElectricWallEvent>(), Is.Empty);
            Assert.That(_world.WallCommands, Is.Empty);
        }

        // --- N+1: the wall goes away, or the zap (§13, P1, P3) ----------------------------------------

        [Test]
        public void AnyOtherMove_AfterTheWarning_TakesTheWallAway_ItIsNeitherBuiltNorDrawn()
        {
            WarningStandsBehindLeft();

            TurnResult result = Run(BodyMove.Crouch);

            Assert.That(_world.LoadedWalls, Is.Empty);
            Assert.That(result.Timeline.Frames.All(frame => !frame.Pose.LeftWall.IsUp), Is.True);
            Assert.That(result.FinalBoard.State.Walls.Left.IsUp, Is.False);
        }

        [Test]
        public void ASecondStepBack_TheWallStandsFromLoad_TheTouchZaps_TheDummyIsThrownForward_AndTheWallAdvances()
        {
            WarningStandsBehindLeft();
            ScheduleTouch(Side.Left, BodyPart.LeftLeg, TouchStep, WarningX);

            TurnResult result = Run(BodyMove.StepBack);

            Assert.That(_world.LoadedWalls[Side.Left], Is.EqualTo(WarningX), "the wall persists through the board");
            ElectricWallEvent zap = WallEvents(result, ElectricWallPhase.Active).Single();
            Assert.That(zap.Time, Is.EqualTo(_clock.TimeAtStep(TouchStep)));
            Assert.That(zap.HpDamage, Is.EqualTo(Wall.Damage));
            Assert.That(result.FinalBoard.State.Fighters.Left.Hp, Is.EqualTo(_rules.Damage.MaxHp - Wall.Damage));

            float advanced = WarningX + Wall.AdvanceDistanceUnits;
            Assert.That(result.FinalBoard.State.Walls.Left.PositionUnits, Is.EqualTo(advanced).Within(Tolerance), "it moved toward the opponent");
            Assert.That(_world.WallCommands.Count, Is.GreaterThan(1), "the advance plays over several steps");
            Assert.That(_world.WallCommands.Last().XUnits.Value, Is.EqualTo(advanced).Within(Tolerance));
            Assert.That(result.FinalBoard.Pose.LeftWall.XUnits, Is.EqualTo(advanced).Within(Tolerance));

            // The step back alone would end at −450; the bounce throws it forward and the wall's advance shoves it on, in front of the wall.
            Assert.That(FinalTorsoX(result, Side.Left), Is.EqualTo(advanced + TorsoHalfWidth).Within(Tolerance));
            Assert.That(FinalTorsoX(result, Side.Left), Is.GreaterThan(LeftX), "two retreats in a row end ahead of where the dummy started");
        }

        [Test]
        public void OneRetreat_ZapsOnce_ASecondBodyPartTouchingIsNotANewZap()
        {
            WarningStandsBehindLeft();
            ScheduleTouch(Side.Left, BodyPart.LeftLeg, TouchStep, WarningX);
            ScheduleTouch(Side.Left, BodyPart.Torso, TouchStep + 4, WarningX);

            TurnResult result = Run(BodyMove.StepBack);

            Assert.That(WallEvents(result, ElectricWallPhase.Active), Has.Count.EqualTo(1));
            Assert.That(result.FinalBoard.State.Fighters.Left.Hp, Is.EqualTo(_rules.Damage.MaxHp - Wall.Damage));
        }

        [Test]
        public void OnlyTheOwnersBody_Zaps_NotItsWeapon_NotTheOpponent()
        {
            WarningStandsBehindLeft();
            var wallTag = new BodyTag(Side.Left, BodyRole.ElectricWall, BodyPart.Torso);
            _world.ScheduleContacts(TouchStep,
                new ContactFacts(wallTag, new BodyTag(Side.Left, BodyRole.HeldItem, BodyPart.Torso), new Vec2(WarningX, 100f), Vec2.UnitX, Vec2.Zero),
                new ContactFacts(wallTag, new BodyTag(Side.Right, BodyRole.BodyPart, BodyPart.Torso), new Vec2(WarningX, 100f), Vec2.UnitX, Vec2.Zero));

            TurnResult result = Run(BodyMove.StepBack);

            ElectricWallEvent zap = WallEvents(result, ElectricWallPhase.Active).Single();
            Assert.That(zap.Time, Is.GreaterThan(_clock.TimeAtStep(TouchStep + 2)), "not at the scripted touches: the step back's owed zap");
            Assert.That(result.FinalBoard.State.Fighters.Right.Hp, Is.EqualTo(_rules.Damage.MaxHp));
        }

        [Test]
        public void AStepBackThatNeverReachesItsWall_IsStillZapped_JustAfterItEnds()
        {
            WarningStandsBehindLeft();
            double moveEnds = _rules.FindBodyMove(BodyMove.StepBack).DurationSeconds;

            TurnResult result = Run(BodyMove.StepBack);

            ElectricWallEvent zap = WallEvents(result, ElectricWallPhase.Active).Single();
            Assert.That(zap.Time.Seconds, Is.GreaterThan(moveEnds), "only the input counts: the second retreat is always zapped");
            Assert.That(zap.Time.Seconds, Is.LessThan(moveEnds + 0.15), "a moment after the step back ends");
            Assert.That(FinalTorsoX(result, Side.Left), Is.EqualTo(WarningX + Wall.AdvanceDistanceUnits + TorsoHalfWidth).Within(Tolerance),
                "and it is thrown forward all the same");
        }

        [Test]
        public void AZapThatKills_LeavesTheDummyLimp_WithItsTorsoSplit()
        {
            WarningStandsBehindLeft();
            _state.Fighters.Left.Hp = 4f;
            ScheduleTouch(Side.Left, BodyPart.LeftLeg, TouchStep, WarningX);

            TurnResult result = Run(BodyMove.StepBack);

            Assert.That(result.FinalBoard.State.Fighters.Left.IsDead, Is.True);
            FighterDiedEvent died = result.Timeline.Events.OfType<FighterDiedEvent>().Single();
            Assert.That((died.Side, died.Time), Is.EqualTo((Side.Left, _clock.TimeAtStep(TouchStep))));
            Assert.That(result.Report.DeathTime.Left, Is.EqualTo(_clock.TimeAtStep(TouchStep)), "sudden death can tell who fell first (P4)");
            Assert.That(result.Report.DeathTime.Right, Is.Null);
            Assert.That(_world.Splits, Is.EqualTo(new[] { Side.Left }));
            Assert.That(_world.Drops, Is.EqualTo(new[] { Side.Left }), "a dead dummy lets go of its weapon");
        }

        [Test]
        public void BothWalls_ZapInTheSameTurn_EachAdvancingTowardItsOpponent()
        {
            WarningStandsBehindLeft();
            float rightWarning = RightX + Wall.SpawnOffsetBehindUnits;
            _wallRules.OnPlansLocked(_state.Walls.Right, BodyMove.StepBack, false, Wall);
            _wallRules.RaiseWarning(SimTime.Zero, Side.Right, _state.Walls.Right, RightX, new ArenaEdges(-1000f, 1000f, true), Wall);
            ScheduleTouch(Side.Left, BodyPart.LeftLeg, TouchStep, WarningX);
            ScheduleTouch(Side.Right, BodyPart.RightLeg, TouchStep, rightWarning);

            TurnResult result = Run(BodyMove.StepBack, BodyMove.StepBack);

            Assert.That(WallEvents(result, ElectricWallPhase.Active).Select(e => e.Side), Is.EquivalentTo(new[] { Side.Left, Side.Right }));
            Assert.That(result.FinalBoard.State.Walls.Left.PositionUnits, Is.EqualTo(WarningX + Wall.AdvanceDistanceUnits).Within(Tolerance));
            Assert.That(result.FinalBoard.State.Walls.Right.PositionUnits, Is.EqualTo(rightWarning - Wall.AdvanceDistanceUnits).Within(Tolerance));
        }

        [Test]
        public void TheWall_PersistsAcrossTurns_ThroughTheBoardSnapshot()
        {
            TurnResult first = Run(BodyMove.StepBack);
            float raisedX = first.FinalBoard.State.Walls.Left.PositionUnits;
            BoardSnapshot board = first.FinalBoard.Clone();
            ScheduleTouch(Side.Left, BodyPart.LeftLeg, TouchStep, raisedX);

            TurnResult second = Run(BodyMove.StepBack, BodyMove.None, board.State, board.Pose);

            Assert.That(_world.LoadedWalls[Side.Left], Is.EqualTo(raisedX), "rebuilt by Load from the board");
            Assert.That(second.Timeline.Frames[0].Pose.LeftWall.XUnits, Is.EqualTo(raisedX));
            Assert.That(WallEvents(second, ElectricWallPhase.Active), Has.Count.EqualTo(1));
        }

        // --- TurnWalls on its own --------------------------------------------------------------------

        private TurnWalls ActiveLeftWall()
        {
            WarningStandsBehindLeft();
            _wallRules.OnPlansLocked(_state.Walls.Left, BodyMove.StepBack, false, Wall);
            var walls = new TurnWalls(_world);
            walls.Begin(_state, new ArenaEdges(-1000f, 1000f, true), Wall, 0.15f);
            return walls;
        }

        private static SimTime At(double seconds) => SimTime.FromSeconds(seconds);

        [Test]
        public void TheKnockedIntoFlag_ALaterTouchAfterAHitKnock_DealsDamageOnly_OncePerKnock()
        {
            Wall.DamageWhenKnockedInto = true;
            TurnWalls walls = ActiveLeftWall();

            ElectricWallZap zap = walls.Touch(Side.Left, At(0.05), null);
            Assert.That(zap.IsKnockedInto, Is.False);
            Assert.That(walls.Touch(Side.Left, At(0.1), null), Is.Null, "pressing on, no hit since the zap");
            Assert.That(walls.Touch(Side.Left, At(0.1), At(0.04)), Is.Null, "that hit came before the zap");

            ElectricWallZap knocked = walls.Touch(Side.Left, At(0.3), At(0.2));
            Assert.That(knocked.IsKnockedInto, Is.True);
            Assert.That(knocked.HpDamage, Is.EqualTo(Wall.Damage));
            Assert.That(knocked.BounceDistanceUnits, Is.EqualTo(0f));
            Assert.That(walls.Touch(Side.Left, At(0.35), At(0.2)), Is.Null, "once per knock");
            Assert.That(_state.Fighters.Left.Hp, Is.EqualTo(_rules.Damage.MaxHp - 2f * Wall.Damage));
        }

        [Test]
        public void WithoutTheKnockedIntoFlag_AHitKnockingTheDummyIntoTheWallDoesNothing()
        {
            TurnWalls walls = ActiveLeftWall();
            walls.Touch(Side.Left, At(0.05), null);

            Assert.That(walls.Touch(Side.Left, At(0.3), At(0.2)), Is.Null);
        }

        [Test]
        public void AWarning_HoldsItsDummyBack_FromWhenItSpringsUp()
        {
            _wallRules.OnPlansLocked(_state.Walls.Left, BodyMove.StepBack, false, Wall);
            var walls = new TurnWalls(_world);
            walls.Begin(_state, new ArenaEdges(-1000f, 1000f, true), Wall, 0.15f);

            Assert.That(walls.BackLimitAt(Side.Left, At(0.1)), Is.Null);
            walls.Raise(Side.Left, At(0.3), LeftX - StepBackUnits);

            Assert.That(walls.BackLimitAt(Side.Left, At(0.3)), Is.EqualTo(LeftX - StepBackUnits - Wall.SpawnOffsetBehindUnits).Within(Tolerance));
            Assert.That(walls.IsRaisePending(Side.Left), Is.False);
        }

        [Test]
        public void AnActiveWall_HoldsItsDummyBack_OnlyFromItsZap_ThenAdvancing()
        {
            TurnWalls walls = ActiveLeftWall();

            Assert.That(walls.BackLimitAt(Side.Left, At(0.01)), Is.Null, "the retreat runs into it");
            Assert.That(walls.IsOwedAZap(Side.Left), Is.True);
            walls.Touch(Side.Left, At(0.05), null);

            Assert.That(walls.IsOwedAZap(Side.Left), Is.False);
            Assert.That(walls.BackLimitAt(Side.Left, At(0.05)), Is.EqualTo(WarningX).Within(Tolerance));
            float midway = walls.BackLimitAt(Side.Left, At(0.1)).Value;
            Assert.That(midway, Is.GreaterThan(WarningX).And.LessThan(WarningX + Wall.AdvanceDistanceUnits));
            Assert.That(walls.BackLimitAt(Side.Left, At(1.0)), Is.EqualTo(WarningX + Wall.AdvanceDistanceUnits).Within(Tolerance));
        }

        [Test]
        public void ADeadDummy_IsOwedNoZap()
        {
            TurnWalls walls = ActiveLeftWall();
            _state.Fighters.Left.Hp = 0f;

            Assert.That(walls.Touch(Side.Left, At(0.05), null), Is.Null);
        }

        [Test]
        public void OnlyAPlayersBody_TouchingTheirOwnWall_IsATouch()
        {
            var leftWall = new BodyTag(Side.Left, BodyRole.ElectricWall, BodyPart.Torso);

            Assert.That(TurnWalls.TryReadTouch(Facts(leftWall, new BodyTag(Side.Left, BodyRole.BodyPart, BodyPart.LeftLeg)), out Side owner), Is.True);
            Assert.That(owner, Is.EqualTo(Side.Left));
            Assert.That(TurnWalls.TryReadTouch(Facts(new BodyTag(Side.Left, BodyRole.BodyPart, BodyPart.Head), leftWall), out _), Is.True, "either order");
            Assert.That(TurnWalls.TryReadTouch(Facts(leftWall, new BodyTag(Side.Right, BodyRole.BodyPart, BodyPart.Torso)), out _), Is.False);
            Assert.That(TurnWalls.TryReadTouch(Facts(leftWall, new BodyTag(Side.Left, BodyRole.HeldItem, BodyPart.Torso)), out _), Is.False);
            Assert.That(TurnWalls.TryReadTouch(Facts(leftWall, new BodyTag(Side.Left, BodyRole.SeveredLimb, BodyPart.LeftArm)), out _), Is.False);
            Assert.That(TurnWalls.TryReadTouch(Facts(new BodyTag(Side.Left, BodyRole.BodyPart, BodyPart.Torso),
                new BodyTag(Side.Right, BodyRole.BodyPart, BodyPart.Torso)), out _), Is.False);
        }

        private static ContactFacts Facts(BodyTag a, BodyTag b) => new ContactFacts(a, b, Vec2.Zero, Vec2.UnitX, Vec2.Zero);

        [Test]
        public void APoseSnapshotClone_KeepsTheWalls()
        {
            var pose = new PoseSnapshot { LeftWall = new ElectricWallPose(ElectricWallPhase.Active, -300f) };

            PoseSnapshot copy = pose.Clone();

            Assert.That((copy.LeftWall.IsUp, copy.LeftWall.Phase, copy.LeftWall.XUnits), Is.EqualTo((true, ElectricWallPhase.Active, -300f)));
            Assert.That(copy.GetWall(Side.Right).IsUp, Is.False);
        }

        // --- The contact handler's view ------------------------------------------------------------

        [Test]
        public void Contacts_ReachTheHandler_OneInstantAtATime()
        {
            var handler = new InstantRecordingHandler();
            _world.ScheduleContacts(5, Bodies(Side.Left, Side.Right));
            _world.ScheduleContacts(6, Bodies(Side.Right, Side.Left));
            var simulator = new TurnSimulator(_world, new RulePolicies(), new TurnSimulatorOptions { Contacts = handler });

            simulator.Simulate(new TurnInput(new BoardSnapshot(_state, _pose),
                new PerSide<TurnPlan>(Plan(BodyMove.None), Plan(BodyMove.None)), _rules, _simulation));

            // Both are still waiting when the idle turn settles at step 6, so they are handed over together at its end: in two calls.
            Assert.That(handler.Batches, Has.Count.EqualTo(2));
            Assert.That(handler.Batches.Sum(batch => batch.Count), Is.EqualTo(2));
            Assert.That(handler.Batches.All(batch => batch.Select(contact => contact.Time).Distinct().Count() == 1), Is.True);
        }

        private static ContactFacts Bodies(Side a, Side b) => new ContactFacts(new BodyTag(a, BodyRole.BodyPart, BodyPart.Torso),
            new BodyTag(b, BodyRole.BodyPart, BodyPart.Torso), Vec2.Zero, Vec2.UnitX, Vec2.Zero);

        private sealed class InstantRecordingHandler : ITurnContactHandler
        {
            public List<List<TurnContact>> Batches { get; } = new List<List<TurnContact>>();

            public void BeginTurn(TurnContactContext context)
            {
            }

            public void Handle(IReadOnlyList<TurnContact> contacts, TurnContactContext context) => Batches.Add(contacts.ToList());
        }
    }
}
