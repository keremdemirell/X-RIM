using System;
using System.Collections.Generic;
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
using XRim.Simulation.Drivers;
using XRim.Simulation.Execution;
using XRim.Simulation.Physics;
using XRim.Simulation.Recording;
using XRim.Simulation.Settings;

namespace XRim.Tests.EditMode.Simulation.Execution
{
    /// <summary>
    /// The turn loop on the fake world (ARCHITECTURE §6). The left dummy's pelvis is at the origin, so with the default
    /// shoulder (0, 100) a rapier thrust along y = 100 has its tip at (400 + v·t, 100). The right dummy stands at x = 600,
    /// inside the arena's solid edges (±1000).
    /// </summary>
    public sealed class TurnSimulatorTests
    {
        private const float PathStartX = 400f;
        private const float ShoulderHeight = 100f;
        private const float RightX = 600f;
        private const int TurnIndex = 3;
        private static readonly WeaponPath StraightThrust =
            new WeaponPath(new[] { new Vec2(PathStartX, ShoulderHeight), new Vec2(640f, ShoulderHeight) });

        private readonly WeaponStats _rapier = GddStartingValues.Rapier();
        private RulesSettings _rules;
        private SimulationSettings _simulation;
        private FakePhysicsWorld _world;
        private SimClock _clock;
        private CapturingContactHandler _handler;
        private PoseSnapshot _pose;
        private MatchState _state;

        [SetUp]
        public void SetUp()
        {
            // NUnit reuses one fixture instance, so anything a test may change is rebuilt here.
            _rules = GddStartingValues.CreateRulesSettings();
            _simulation = new SimulationSettings();
            _world = new FakePhysicsWorld();
            _clock = new SimClock(_simulation.StepRateHz);
            _handler = new CapturingContactHandler(_world);
            _state = new MatchState(
                PerSide<FighterState>.Create(_ => new FighterState(Handedness.Right, _rules.Damage.MaxHp, WeaponIds.Rapier)),
                PerSide<ElectricWallState>.Create(_ => new ElectricWallState())) { TurnIndex = TurnIndex };
            _pose = new PoseSnapshot();
            _pose.Left.Set(BodyPart.Torso, new BodyPose(Vec2.Zero, 0f));
            _pose.Right.Set(BodyPart.Torso, new BodyPose(new Vec2(RightX, 0f), 0f));
            _pose.Left.HeldItem = new BodyPose(new Vec2(0f, ShoulderHeight), 0f);
            _pose.Right.HeldItem = new BodyPose(new Vec2(RightX, ShoulderHeight), 180f);
        }

        [TearDown]
        public void TearDown() => _world.Dispose();

        private static TurnPlan Plan(WeaponPath path, BodyMove move = BodyMove.None) =>
            new TurnPlan(WeaponIds.Rapier, move, path ?? WeaponPath.Empty, default, true);

        private TurnInput Input(WeaponPath leftPath, WeaponPath rightPath = null) =>
            new TurnInput(new BoardSnapshot(_state, _pose), new PerSide<TurnPlan>(Plan(leftPath), Plan(rightPath)), _rules, _simulation);

        private TurnSimulator Simulator(IBodyMoveDriverFactory bodyMoves = null) =>
            new TurnSimulator(_world, new RulePolicies(), new TurnSimulatorOptions { Contacts = _handler, BodyMoves = bodyMoves });

        private TurnResult Run(WeaponPath leftPath, WeaponPath rightPath = null) => Simulator().Simulate(Input(leftPath, rightPath));

        private static int StepsSimulated(TurnResult result) => result.Timeline.Frames[result.Timeline.Frames.Count - 1].Step;

        private Vec2 Tip(BodyPose grip) => grip.PositionUnits + Vec2.FromAngleDegrees(grip.RotationDegrees) * _rapier.LengthUnits;

        private Vec2 TipAt(double seconds) => new Vec2(PathStartX + (float)(_rapier.SpeedUnitsPerSecond * seconds), ShoulderHeight);

        /// <summary>Where the rapier's blade touches the point when its tip is at <see cref="TipAt"/>(seconds): half its width ahead.</summary>
        private Vec2 PointTouchedAt(double seconds) => TipAt(seconds) + new Vec2(_rapier.InkThicknessUnits * 0.5f, 0f);

        private static ContactFacts RapierHitsTorso(Vec2 point) => new ContactFacts(
            new BodyTag(Side.Left, BodyRole.HeldItem, BodyPart.Torso),
            new BodyTag(Side.Right, BodyRole.BodyPart, BodyPart.Torso),
            point, Vec2.UnitX, new Vec2(-900f, 0f));

        private static ContactFacts Bodies(Side ownerA, BodyPart partA, Side ownerB, BodyPart partB) => new ContactFacts(
            new BodyTag(ownerA, BodyRole.BodyPart, partA), new BodyTag(ownerB, BodyRole.BodyPart, partB),
            new Vec2(500f, 100f), Vec2.UnitX, Vec2.Zero);

        [Test]
        public void Steps_RunAtTheFixedRateOnTheSimClock()
        {
            TurnResult result = Run(StraightThrust);

            IReadOnlyList<TimelineFrame> frames = result.Timeline.Frames;
            Assert.That(frames[0].Step, Is.EqualTo(0));
            Assert.That(frames[0].Time, Is.EqualTo(SimTime.Zero));
            for (int i = 1; i < frames.Count; i++)
            {
                Assert.That(frames[i].Step, Is.EqualTo(i), "one frame per step by default");
                Assert.That(frames[i].Time, Is.EqualTo(_clock.TimeAtStep(i)), "time comes from the step index at 240 Hz");
            }
        }

        [Test]
        public void Kinematic_TipReachesThePathEndAtLengthOverSpeed()
        {
            TurnResult result = Run(StraightThrust);

            int expectedStep = _clock.StepsFor(StraightThrust.LengthUnits / _rapier.SpeedUnitsPerSecond);
            Vec2 end = StraightThrust.Points[1];
            TimelineFrame arrival = null;
            foreach (TimelineFrame frame in result.Timeline.Frames)
            {
                if (Vec2.Distance(Tip(frame.Pose.Left.HeldItem), end) < 0.5f)
                {
                    arrival = frame;
                    break;
                }
            }

            Assert.That(arrival, Is.Not.Null, "the tip reaches the end of the path");
            Assert.That(arrival.Step, Is.InRange(expectedStep - 1, expectedStep + 1), "t = length / speed, ± one step");
            Assert.That(result.EndReason, Is.EqualTo(TurnEndReason.Settled));
            Assert.That(StepsSimulated(result), Is.EqualTo(expectedStep + _simulation.SettleStepsRequired - 1).Within(1));
        }

        [Test]
        public void IdleTurn_EndsOncePhysicsHasSettledForTheRequiredSteps()
        {
            TurnResult result = Run(null);

            Assert.That(result.EndReason, Is.EqualTo(TurnEndReason.Settled));
            Assert.That(StepsSimulated(result), Is.EqualTo(_simulation.SettleStepsRequired));
        }

        [Test]
        public void PathsDoneButPhysicsStillMoving_KeepsRunningUntilItSettles()
        {
            const int settlesAt = 150;
            _world.SettlesAtStep = settlesAt;

            TurnResult result = Run(StraightThrust);

            Assert.That(result.EndReason, Is.EqualTo(TurnEndReason.Settled));
            Assert.That(StepsSimulated(result), Is.EqualTo(settlesAt + _simulation.SettleStepsRequired - 1));
        }

        [Test]
        public void PhysicsThatNeverSettles_EndsAtTheHardCap()
        {
            _world.SettlesAtStep = int.MaxValue;

            TurnResult result = Run(StraightThrust);

            Assert.That(result.EndReason, Is.EqualTo(TurnEndReason.HardCap));
            Assert.That(StepsSimulated(result), Is.EqualTo(_clock.StepsFor(_rules.Match.ExecutionHardCapSeconds)));
            Assert.That(result.Timeline.Duration, Is.EqualTo(SimTime.FromSeconds(_rules.Match.ExecutionHardCapSeconds)));
        }

        [Test]
        public void PathLongerThanTheHardCap_StopsAtTheCap()
        {
            var longPath = new WeaponPath(new[] { new Vec2(PathStartX, ShoulderHeight), new Vec2(PathStartX, 2000f) });

            TurnResult result = Run(longPath);

            Assert.That(result.EndReason, Is.EqualTo(TurnEndReason.HardCap));
            Assert.That(StepsSimulated(result), Is.EqualTo(_clock.StepsFor(_rules.Match.ExecutionHardCapSeconds)));
        }

        [Test]
        public void TunableHardCap_IsRespected()
        {
            _rules.Match.ExecutionHardCapSeconds = 0.5f;
            _world.SettlesAtStep = int.MaxValue;

            TurnResult result = Run(StraightThrust);

            Assert.That(StepsSimulated(result), Is.EqualTo(_clock.StepsFor(0.5)));
        }

        [Test]
        public void WeaponContact_IsRefinedToPathProgressTime()
        {
            const double touchSeconds = 0.1021;
            int touchStep = _clock.StepsFor(touchSeconds);
            _world.ScheduleContacts(touchStep + 1, RapierHitsTorso(PointTouchedAt(touchSeconds)));

            Run(StraightThrust);

            TurnContact contact = _handler.Contacts[0];
            SimTime touch = SimTime.FromSeconds(touchSeconds);
            Assert.That(contact.ReportedStep, Is.EqualTo(touchStep + 1), "Box2D reports it one step late");
            Assert.That(contact.Time.Microseconds,
                Is.InRange(_clock.TimeAtStep(touchStep - 1).Microseconds, _clock.TimeAtStep(touchStep).Microseconds),
                "the refined time lies in the step whose motion made the contact");
            Assert.That(Math.Abs(contact.Time.Microseconds - touch.Microseconds), Is.LessThan(20L));
            Assert.That(contact.PathDistanceUnits, Is.EqualTo(_rapier.SpeedUnitsPerSecond * touchSeconds).Within(0.1f), "d = v·t");
            Assert.That(contact.WeaponSide, Is.EqualTo(Side.Left));
            Assert.That(contact.ContactAngleDegrees, Is.EqualTo(90f).Within(1e-3f), "a thrust straight into the surface");
        }

        [Test]
        public void ContactsInOneStep_AreHandledInImpactTimeOrder()
        {
            // Both touches happen in the same step, so both lie inside the two-step refinement window.
            const double early = 0.1045;
            const double late = 0.1080;
            int reportStep = _clock.StepsFor(late) + 1;
            _world.ScheduleContacts(reportStep, RapierHitsTorso(PointTouchedAt(late)), RapierHitsTorso(PointTouchedAt(early)));

            Run(StraightThrust);

            Assert.That(_handler.Contacts.Count, Is.EqualTo(2));
            Assert.That(_handler.Contacts[0].Time, Is.LessThan(_handler.Contacts[1].Time));
            Assert.That(_handler.Contacts[0].PathDistanceUnits, Is.EqualTo(_rapier.SpeedUnitsPerSecond * early).Within(0.1f));
        }

        [Test]
        public void ContactsAtTheSameTime_AreHandledInStableIdOrder()
        {
            // Body-to-body contacts take their step's time, so these three tie; the engine reported them out of order.
            ContactFacts rightFirst = Bodies(Side.Right, BodyPart.Torso, Side.Left, BodyPart.LeftArm);
            ContactFacts leftTorso = Bodies(Side.Left, BodyPart.Torso, Side.Right, BodyPart.LeftArm);
            ContactFacts leftHead = Bodies(Side.Left, BodyPart.Head, Side.Right, BodyPart.Torso);
            _world.ScheduleContacts(5, rightFirst, leftTorso, leftHead);

            Run(StraightThrust);

            Assert.That(_handler.Contacts.Count, Is.EqualTo(3));
            Assert.That(_handler.Contacts[0].Facts.A.Part, Is.EqualTo(BodyPart.Head), "left before right, then head before torso");
            Assert.That(_handler.Contacts[1].Facts.A.Part, Is.EqualTo(BodyPart.Torso));
            Assert.That(_handler.Contacts[1].Facts.A.Owner, Is.EqualTo(Side.Left));
            Assert.That(_handler.Contacts[2].Facts.A.Owner, Is.EqualTo(Side.Right));
        }

        [Test]
        public void BodyContact_UsesTheReportingStepsTime()
        {
            _world.ScheduleContacts(5, Bodies(Side.Left, BodyPart.Torso, Side.Right, BodyPart.LeftArm));

            Run(StraightThrust);

            TurnContact contact = _handler.Contacts[0];
            Assert.That(contact.WeaponSide, Is.Null);
            Assert.That(contact.Time, Is.EqualTo(_clock.TimeAtStep(5)));
            Assert.That(contact.PathDistanceUnits, Is.EqualTo(0f));
        }

        [Test]
        public void EachStepsContacts_AreHandledAfterThatStep()
        {
            _world.ScheduleContacts(8, Bodies(Side.Left, BodyPart.Torso, Side.Right, BodyPart.LeftArm));
            _world.ScheduleContacts(5, Bodies(Side.Right, BodyPart.Torso, Side.Left, BodyPart.LeftArm));

            Run(StraightThrust);

            Assert.That(_handler.Contacts[0].ReportedStep, Is.EqualTo(5));
            Assert.That(_handler.Contacts[1].ReportedStep, Is.EqualTo(8));
            Assert.That(_handler.StepsSeenAtHandling, Is.EqualTo(new[] { 5, 8 }), "handled right after the step that reported them");
        }

        [Test]
        public void DefaultHandler_RecordsEveryContactAsATimelineEvent()
        {
            _world.ScheduleContacts(5, Bodies(Side.Left, BodyPart.Torso, Side.Right, BodyPart.LeftArm));
            _world.ScheduleContacts(9, RapierHitsTorso(PointTouchedAt(0.0335)));

            TurnResult result = new TurnSimulator(_world, new RulePolicies()).Simulate(Input(StraightThrust));

            IReadOnlyList<MatchEvent> events = result.Timeline.Events;
            Assert.That(events.Count, Is.EqualTo(2));
            Assert.That(events[0], Is.TypeOf<ContactEvent>());
            Assert.That(events[1], Is.TypeOf<ContactEvent>());
            Assert.That(((ContactEvent)events[1]).Contact.WeaponSide, Is.EqualTo(Side.Left));
            Assert.That(events[0].Time, Is.LessThanOrEqualTo(events[1].Time));
        }

        [Test]
        public void Result_CarriesTheTurnIndexTheLastPoseAndACopyOfTheState()
        {
            TurnResult result = Run(StraightThrust);

            TimelineFrame last = result.Timeline.Frames[result.Timeline.Frames.Count - 1];
            Assert.That(result.TurnIndex, Is.EqualTo(TurnIndex));
            Assert.That(result.FinalBoard.Pose.Left.HeldItem.PositionUnits, Is.EqualTo(last.Pose.Left.HeldItem.PositionUnits));
            Assert.That(result.FinalBoard.Pose, Is.Not.SameAs(last.Pose), "the next board is independent of the recording");
            Assert.That(result.FinalBoard.State, Is.Not.SameAs(_state), "the input board is never changed");
            Assert.That(result.Report.ResolvedState, Is.Not.SameAs(_state));
            Assert.That(result.Report.ResolvedState.TurnIndex, Is.EqualTo(TurnIndex));
            Assert.That(result.Report.FirstValidHitTime.Left, Is.Null, "no hit rules yet (Session 06)");
        }

        [Test]
        public void World_IsLoadedOnceWithTheFrozenBoardAndTheSettings()
        {
            Run(StraightThrust);

            Assert.That(_world.LoadCount, Is.EqualTo(1));
            Assert.That(_world.LoadedPose, Is.SameAs(_pose));
            Assert.That(_world.LoadedSimulation, Is.SameAs(_simulation));
        }

        [Test]
        public void NoBodyMove_HoldsTheFrozenSpotStandingUpright()
        {
            _pose.Right.Set(BodyPart.Torso, new BodyPose(new Vec2(RightX, 0f), 12f));

            TurnResult result = Run(StraightThrust);

            BodyPose torso = result.FinalBoard.Pose.Right.Get(BodyPart.Torso);
            Assert.That(torso.PositionUnits, Is.EqualTo(new Vec2(RightX, 0f)));
            Assert.That(torso.RotationDegrees, Is.EqualTo(TurnStartRoot.UprightDegrees));
        }

        [Test]
        public void PathFrame_StaysUprightWhenTheTorsoStartsTilted()
        {
            _pose.Left.Set(BodyPart.Torso, new BodyPose(Vec2.Zero, 15f));

            TurnResult result = Run(StraightThrust);

            Assert.That(Vec2.Distance(Tip(result.FinalBoard.Pose.Left.HeldItem), StraightThrust.Points[1]), Is.LessThan(0.5f),
                "the path is drawn upright, so it ends where it was drawn");
        }

        [Test]
        public void SideWithoutAPath_HoldsItsWeapon()
        {
            TurnResult result = Run(StraightThrust);

            Assert.That(result.FinalBoard.Pose.Right.HeldItem.PositionUnits, Is.EqualTo(new Vec2(RightX, ShoulderHeight)));
        }

        [Test]
        public void BodyMoves_ComeFromTheFactory_StartAtTheFrozenRoot_AndMustFinish()
        {
            const float slideUnits = 120f;
            const double moveSeconds = 0.5;
            var factory = new SlidingMoveFactory(slideUnits, moveSeconds);
            var plans = new PerSide<TurnPlan>(Plan(null, BodyMove.Lunge), Plan(null));

            TurnResult result = Simulator(factory).Simulate(new TurnInput(new BoardSnapshot(_state, _pose), plans, _rules, _simulation));

            Assert.That(factory.Requested, Is.EqualTo(new[] { BodyMove.Lunge, BodyMove.None }));
            Assert.That(factory.StartRoots[0].PositionUnits, Is.EqualTo(Vec2.Zero));
            Assert.That(StepsSimulated(result), Is.EqualTo(_clock.StepsFor(moveSeconds) + _simulation.SettleStepsRequired - 1),
                "the turn waits for the body move");
            FighterPose left = result.FinalBoard.Pose.Left;
            Assert.That(left.Get(BodyPart.Torso).PositionUnits.X, Is.EqualTo(slideUnits).Within(1e-3f));
            Assert.That(left.HeldItem.PositionUnits.X, Is.EqualTo(slideUnits).Within(1e-3f), "a held weapon travels with the body");
        }

        [Test]
        public void World_GetsTheArenaEdgesFromThePolicy()
        {
            _rules.Arena.WidthUnits = 1600f;

            Run(StraightThrust);

            Assert.That(_world.LoadedEdges.LeftXUnits, Is.EqualTo(-800f));
            Assert.That(_world.LoadedEdges.RightXUnits, Is.EqualTo(800f));
            Assert.That(_world.LoadedEdges.IsSolid, Is.True, "D22: solid invisible stops");
        }

        [Test]
        public void BodyMoveTowardASolidEdge_StopsTheRootHalfATorsoInside()
        {
            // The right dummy stands 100 units from the right edge; its move slides its root 400 units outward.
            float edge = _rules.Arena.WidthUnits * 0.5f;
            _pose.Right.Set(BodyPart.Torso, new BodyPose(new Vec2(edge - 100f, 0f), 0f));
            var factory = new SlidingMoveFactory(400f, 0.2);
            var plans = new PerSide<TurnPlan>(Plan(null), Plan(null, BodyMove.StepBack));

            Simulator(factory).Simulate(new TurnInput(new BoardSnapshot(_state, _pose), plans, _rules, _simulation));

            float limit = edge - _simulation.Ragdoll.TorsoWidthUnits * 0.5f;
            foreach ((Side side, BodyPose pose) in _world.RootTargets)
            {
                if (side == Side.Right) Assert.That(pose.PositionUnits.X, Is.LessThanOrEqualTo(limit));
            }

            Assert.That(_world.RootTargets[_world.RootTargets.Count - 1].Pose.PositionUnits.X, Is.EqualTo(limit));
        }

        // --- Session 05: the game's body moves (GDD §5) -----------------------------------------------

        /// <summary>The left dummy stands on its legs: pelvis one leg length above the floor, weapon at shoulder height.</summary>
        private void StandLeftDummyUp()
        {
            _pose.Left.Set(BodyPart.Torso, new BodyPose(new Vec2(0f, StandingHeight), 0f));
            _pose.Left.HeldItem = new BodyPose(new Vec2(0f, StandingHeight + ShoulderHeight), 0f);
        }

        private float StandingHeight => _simulation.Ragdoll.LegLengthUnits;

        private TurnResult RunWithMoves(BodyMove left, BodyMove right, WeaponPath leftPath = null) =>
            Simulator().Simulate(new TurnInput(new BoardSnapshot(_state, _pose),
                new PerSide<TurnPlan>(Plan(leftPath, left), Plan(null, right)), _rules, _simulation));

        private BodyPose LastRootTarget(Side side)
        {
            for (int i = _world.RootTargets.Count - 1; i >= 0; i--)
            {
                if (_world.RootTargets[i].Side == side) return _world.RootTargets[i].Pose;
            }

            throw new AssertionException($"no root target for {side}");
        }

        private LimbAngles LastLimbTarget(Side side, BodyPart limb)
        {
            for (int i = _world.LimbTargets.Count - 1; i >= 0; i--)
            {
                if (_world.LimbTargets[i].Side == side && _world.LimbTargets[i].Limb == limb) return _world.LimbTargets[i].Target;
            }

            throw new AssertionException($"no target for {side} {limb}");
        }

        [Test]
        public void EveryStep_PosesBothLegsOfBothDummies_AndNoArm()
        {
            TurnResult result = Run(StraightThrust);

            Assert.That(_world.LimbTargets.Count, Is.EqualTo(StepsSimulated(result) * 4), "two legs, two dummies, every step");
            Assert.That(_world.LimbTargets.TrueForAll(target => target.Limb.IsLeg()), Is.True);
        }

        [Test]
        public void SeveredLeg_IsNotPosed()
        {
            _state.Fighters[Side.Left].MarkSevered(BodyPart.LeftLeg);

            Run(StraightThrust);

            Assert.That(_world.LimbTargets.Exists(target => target.Side == Side.Left && target.Limb == BodyPart.LeftLeg), Is.False);
            Assert.That(_world.LimbTargets.Exists(target => target.Side == Side.Left && target.Limb == BodyPart.RightLeg), Is.True);
        }

        [Test]
        public void Crouch_SinksTheRoot_BendsTheKnees_AndTheTurnWaitsForIt()
        {
            StandLeftDummyUp();
            BodyMoveStats crouch = _rules.FindBodyMove(BodyMove.Crouch);

            TurnResult result = RunWithMoves(BodyMove.Crouch, BodyMove.None);

            Assert.That(LastRootTarget(Side.Left).PositionUnits.Y, Is.EqualTo(StandingHeight + crouch.DisplacementUnits.Y).Within(1e-3f));
            Assert.That(LastLimbTarget(Side.Left, BodyPart.RightLeg).LowerDegrees, Is.LessThan(0f), "the front knee bends");
            Assert.That(LastLimbTarget(Side.Left, BodyPart.LeftLeg).LowerDegrees, Is.LessThan(0f), "the back knee bends");
            Assert.That(StepsSimulated(result), Is.GreaterThanOrEqualTo(_clock.StepsFor(crouch.DurationSeconds)), "the turn waits for the move");
        }

        [Test]
        public void Lunge_CarriesThePathForward_AtTheAngleItWasDrawn()
        {
            StandLeftDummyUp();
            BodyMoveStats lunge = _rules.FindBodyMove(BodyMove.Lunge);

            TurnResult result = RunWithMoves(BodyMove.Lunge, BodyMove.None, StraightThrust);

            BodyPose root = LastRootTarget(Side.Left);
            Assert.That(root.PositionUnits.X, Is.EqualTo(lunge.DisplacementUnits.X).Within(1e-3f), "the lunge stepped forward");
            Assert.That(root.RotationDegrees, Is.EqualTo(-lunge.LeanDegrees).Within(1e-3f), "and leans in");
            Vec2 expected = root.PositionUnits + StraightThrust.Points[1];
            Assert.That(Vec2.Distance(Tip(result.FinalBoard.Pose.Left.HeldItem), expected), Is.LessThan(0.5f),
                "the thrust ends as drawn, carried by the body (§6) and still level");
        }

        [Test]
        public void PathTiltsWithTorsoLean_LetsTheLungeDipTheThrust()
        {
            StandLeftDummyUp();
            _rules.Paths.PathTiltsWithTorsoLean = true;

            TurnResult result = RunWithMoves(BodyMove.Lunge, BodyMove.None, StraightThrust);

            Vec2 expected = TorsoFrame.ToArena(StraightThrust.Points[1], LastRootTarget(Side.Left), Side.Left);
            Assert.That(Vec2.Distance(Tip(result.FinalBoard.Pose.Left.HeldItem), expected), Is.LessThan(0.5f));
            Assert.That(expected.Y, Is.LessThan(LastRootTarget(Side.Left).PositionUnits.Y + ShoulderHeight), "the lean dips it");
        }

        [Test]
        public void WeaponSpeedBonus_D13_SpeedsThePathUpForTheTurn()
        {
            StandLeftDummyUp();
            BodyMoveStats lunge = _rules.FindBodyMove(BodyMove.Lunge);
            lunge.DisplacementUnits = new Vec2(0f, 0f);
            lunge.LeanDegrees = 0f;
            lunge.StrideUnits = 0f;
            lunge.DurationSeconds = 0f;
            lunge.WeaponSpeedBonusFraction = 0.5f;

            TurnResult result = RunWithMoves(BodyMove.Lunge, BodyMove.None, StraightThrust);

            Vec2 end = new Vec2(0f, StandingHeight) + StraightThrust.Points[1];
            int expectedStep = _clock.StepsFor(StraightThrust.LengthUnits / (_rapier.SpeedUnitsPerSecond * 1.5f));
            TimelineFrame arrival = null;
            foreach (TimelineFrame frame in result.Timeline.Frames)
            {
                if (Vec2.Distance(Tip(frame.Pose.Left.HeldItem), end) >= 0.5f) continue;
                arrival = frame;
                break;
            }

            Assert.That(arrival, Is.Not.Null, "the tip reaches the end of the path");
            Assert.That(arrival.Step, Is.InRange(expectedStep - 1, expectedStep + 1), "t = length / (speed × 1.5)");
        }

        [Test]
        public void ChosenBodyMoves_AreEventsAtTheStart_WithTheBackwardFact()
        {
            TurnResult result = RunWithMoves(BodyMove.Crouch, BodyMove.StepBack);

            var started = new List<BodyMoveStartedEvent>();
            foreach (MatchEvent matchEvent in result.Timeline.Events)
            {
                if (matchEvent is BodyMoveStartedEvent moveEvent) started.Add(moveEvent);
            }

            Assert.That(started.Count, Is.EqualTo(2));
            Assert.That(started[0].Time, Is.EqualTo(SimTime.Zero));
            Assert.That((started[0].Side, started[0].Move, started[0].IsBackward), Is.EqualTo((Side.Left, BodyMove.Crouch, false)));
            Assert.That((started[1].Side, started[1].Move, started[1].IsBackward), Is.EqualTo((Side.Right, BodyMove.StepBack, true)),
                "§13: the backward swipe is the player's input");
        }

        [Test]
        public void NoBodyMove_IsNoEvent()
        {
            TurnResult result = Run(StraightThrust);

            foreach (MatchEvent matchEvent in result.Timeline.Events)
            {
                Assert.That(matchEvent, Is.Not.InstanceOf<BodyMoveStartedEvent>());
            }
        }

        [Test]
        public void ContactHandler_SeesEachSidesBodyMove_ForTheD13DamageBonus()
        {
            _world.ScheduleContacts(5, Bodies(Side.Left, BodyPart.Torso, Side.Right, BodyPart.LeftArm));

            RunWithMoves(BodyMove.Lunge, BodyMove.None);

            Assert.That(_handler.LastContext.BodyMoves.Left, Is.SameAs(_rules.FindBodyMove(BodyMove.Lunge)));
            Assert.That(_handler.LastContext.BodyMoves.Right.Move, Is.EqualTo(BodyMove.None));
        }

        [Test]
        public void MotorDriver_FollowsThePathToItsEnd()
        {
            _simulation.WeaponDriver = WeaponDriverKind.Motor;

            TurnResult result = Run(StraightThrust);

            Assert.That(Vec2.Distance(Tip(result.FinalBoard.Pose.Left.HeldItem), StraightThrust.Points[1]), Is.LessThan(20f));
        }

        [Test]
        public void Recording_KeepsEveryNthStepAndTheLast()
        {
            _simulation.RecordEveryNthStep = 4;

            TurnResult result = Run(StraightThrust);

            IReadOnlyList<TimelineFrame> frames = result.Timeline.Frames;
            for (int i = 0; i < frames.Count - 1; i++)
            {
                Assert.That(frames[i].Step, Is.EqualTo(i * 4));
            }

            Assert.That(frames[frames.Count - 1].Pose.Left.HeldItem.PositionUnits,
                Is.EqualTo(result.FinalBoard.Pose.Left.HeldItem.PositionUnits), "the last step is always recorded");
        }

        [Test]
        public void SameInputsTwice_GiveTheSameEventsAndEndPose()
        {
            // The fake world keeps its schedule across loads, so both runs see the same contact. One simulator runs both,
            // which also shows that nothing leaks from one turn into the next.
            _world.ScheduleContacts(9, RapierHitsTorso(PointTouchedAt(0.0335)));
            var simulator = new TurnSimulator(_world, new RulePolicies());
            TurnResult first = simulator.Simulate(Input(StraightThrust));
            TurnResult second = simulator.Simulate(Input(StraightThrust));

            Assert.That(first.Timeline.Events.Count, Is.EqualTo(1));
            Assert.That(second.Timeline.Events.Count, Is.EqualTo(first.Timeline.Events.Count));
            Assert.That(second.Timeline.Events[0].Time, Is.EqualTo(first.Timeline.Events[0].Time));
            Assert.That(second.FinalBoard.Pose.Left.HeldItem.PositionUnits, Is.EqualTo(first.FinalBoard.Pose.Left.HeldItem.PositionUnits));
        }

        /// <summary>Records what reaches the rules' seam, and how many steps the world had taken when it did.</summary>
        private sealed class CapturingContactHandler : ITurnContactHandler
        {
            private readonly FakePhysicsWorld _world;

            public List<TurnContact> Contacts { get; } = new List<TurnContact>();
            public List<int> StepsSeenAtHandling { get; } = new List<int>();

            public CapturingContactHandler(FakePhysicsWorld world)
            {
                _world = world;
            }

            public TurnContactContext LastContext { get; private set; }

            public void Handle(TurnContact contact, TurnContactContext context)
            {
                Contacts.Add(contact);
                StepsSeenAtHandling.Add(_world.StepCount);
                LastContext = context;
            }
        }

        /// <summary>Every move slides the root toward the opponent at a constant rate for a fixed time (a stand-in for Session 05).</summary>
        private sealed class SlidingMoveFactory : IBodyMoveDriverFactory
        {
            private readonly float _slideUnits;
            private readonly double _seconds;

            public List<BodyMove> Requested { get; } = new List<BodyMove>();
            public List<BodyPose> StartRoots { get; } = new List<BodyPose>();

            public SlidingMoveFactory(float slideUnits, double seconds)
            {
                _slideUnits = slideUnits;
                _seconds = seconds;
            }

            public IBodyMoveDriver Create(BodyMove move, RulesSettings rules, SimulationSettings simulation)
            {
                Requested.Add(move);
                return move == BodyMove.None ? (IBodyMoveDriver)new NeutralBodyMoveDriver() : new SlidingMove(this);
            }

            private sealed class SlidingMove : IBodyMoveDriver
            {
                private readonly SlidingMoveFactory _factory;
                private BodyPose _start;

                public SlidingMove(SlidingMoveFactory factory)
                {
                    _factory = factory;
                }

                public void Begin(BodyMoveStats move, BodyMoveStart start)
                {
                    _start = start.Root;
                    _factory.StartRoots.Add(start.Root);
                }

                public BodyMoveFrame Evaluate(SimTime time)
                {
                    double fraction = Math.Min(1.0, time.Seconds / _factory._seconds);
                    var root = new BodyPose(_start.PositionUnits + new Vec2((float)(_factory._slideUnits * fraction), 0f), _start.RotationDegrees);
                    return new BodyMoveFrame(root, root.PositionUnits, root.PositionUnits);
                }

                public bool IsComplete(SimTime time) => time.Seconds >= _factory._seconds;
            }
        }
    }
}
