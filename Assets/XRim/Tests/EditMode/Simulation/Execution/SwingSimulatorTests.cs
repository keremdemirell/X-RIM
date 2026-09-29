using System;
using NUnit.Framework;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Arena;
using XRim.Rules.Match;
using XRim.Rules.Paths;
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
    /// The feel spike's swing loop on the fake world. The left dummy's torso frame is at the origin, so with the default
    /// shoulder (0, 100) a rapier thrust along y = 100 has its tip at (400 + v·t, 100).
    /// </summary>
    public sealed class SwingSimulatorTests
    {
        private const float PathStartX = 400f;
        private const float ShoulderHeight = 100f;
        private static readonly WeaponPath StraightThrust =
            new WeaponPath(new[] { new Vec2(PathStartX, ShoulderHeight), new Vec2(640f, ShoulderHeight) });

        private readonly WeaponStats _rapier = GddStartingValues.Rapier();
        private RulesSettings _rules;
        private SimulationSettings _simulation;
        private FakePhysicsWorld _world;
        private SimClock _clock;

        [SetUp]
        public void SetUp()
        {
            // NUnit reuses one fixture instance, so anything a test may change is rebuilt here.
            _rules = GddStartingValues.CreateRulesSettings();
            _simulation = new SimulationSettings();
            _world = new FakePhysicsWorld();
            _clock = new SimClock(_simulation.StepRateHz);
        }

        [TearDown]
        public void TearDown() => _world.Dispose();

        private SwingResult Run(WeaponPath leftPath, WeaponPath rightPath = null)
        {
            var state = new MatchState(
                PerSide<FighterState>.Create(_ => new FighterState(Handedness.Right, _rules.Damage.MaxHp, WeaponIds.Rapier)),
                PerSide<ElectricWallState>.Create(_ => new ElectricWallState()));
            var pose = new PoseSnapshot();
            pose.Left.Set(BodyPart.Torso, new BodyPose(Vec2.Zero, 0f));
            pose.Right.Set(BodyPart.Torso, new BodyPose(new Vec2(1000f, 0f), 0f));
            pose.Left.HeldItem = new BodyPose(new Vec2(0f, ShoulderHeight), 0f);
            pose.Right.HeldItem = new BodyPose(new Vec2(1000f, ShoulderHeight), 180f);
            var input = new SwingInput(pose, state, new PerSide<WeaponPath>(leftPath, rightPath), _rules, _simulation);
            return new SwingSimulator(_world, new AimFromShoulderModel()).Run(input);
        }

        private Vec2 Tip(BodyPose grip) => grip.PositionUnits + Vec2.FromAngleDegrees(grip.RotationDegrees) * _rapier.LengthUnits;

        private Vec2 TipAt(double seconds) => new Vec2(PathStartX + (float)(_rapier.SpeedUnitsPerSecond * seconds), ShoulderHeight);

        private static ContactFacts RapierHitsTorso(Vec2 point) => new ContactFacts(
            new BodyTag(Side.Left, BodyRole.HeldItem, BodyPart.Torso),
            new BodyTag(Side.Right, BodyRole.BodyPart, BodyPart.Torso),
            point, Vec2.UnitX, new Vec2(-900f, 0f));

        /// <summary>Where the rapier's blade touches the point when its tip is at <see cref="TipAt"/>(seconds): half its width ahead.</summary>
        private Vec2 PointTouchedAt(double seconds) => TipAt(seconds) + new Vec2(_rapier.InkThicknessUnits * 0.5f, 0f);

        [Test]
        public void Kinematic_TipReachesThePathEndAtLengthOverSpeed()
        {
            SwingResult result = Run(StraightThrust);

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
            Assert.That(result.EndReason, Is.EqualTo(SwingEndReason.Settled));
            Assert.That(result.StepsSimulated, Is.EqualTo(expectedStep + _simulation.SettleStepsRequired - 1).Within(1));
        }

        [Test]
        public void WeaponContact_TimeToImpactMatchesTheStepOfTheContact()
        {
            const double touchSeconds = 0.1021;
            int touchStep = _clock.StepsFor(touchSeconds);
            _world.ScheduleContacts(touchStep + 1, RapierHitsTorso(PointTouchedAt(touchSeconds)));

            SwingContact contact = Run(StraightThrust).Contacts[0];

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
        public void ContactsInOneStep_AreOrderedByImpactTime()
        {
            // Both touches happen in the same step, so both lie inside the two-step refinement window.
            const double early = 0.1045;
            const double late = 0.1080;
            int reportStep = _clock.StepsFor(late) + 1;
            _world.ScheduleContacts(reportStep, RapierHitsTorso(PointTouchedAt(late)), RapierHitsTorso(PointTouchedAt(early)));

            SwingResult result = Run(StraightThrust);

            Assert.That(result.Contacts.Count, Is.EqualTo(2));
            Assert.That(result.Contacts[0].Time, Is.LessThan(result.Contacts[1].Time));
            Assert.That(result.Contacts[0].PathDistanceUnits, Is.EqualTo(_rapier.SpeedUnitsPerSecond * early).Within(0.1f));
        }

        [Test]
        public void BodyContact_UsesTheReportingStepsTime()
        {
            var bodies = new ContactFacts(new BodyTag(Side.Left, BodyRole.BodyPart, BodyPart.Torso),
                new BodyTag(Side.Right, BodyRole.BodyPart, BodyPart.LeftArm), new Vec2(500f, 100f), Vec2.UnitX, Vec2.Zero);
            _world.ScheduleContacts(5, bodies);

            SwingContact contact = Run(StraightThrust).Contacts[0];

            Assert.That(contact.WeaponSide, Is.Null);
            Assert.That(contact.Time, Is.EqualTo(_clock.TimeAtStep(5)));
            Assert.That(contact.PathDistanceUnits, Is.EqualTo(0f));
        }

        [Test]
        public void PathLongerThanTheHardCap_StopsAtTheCap()
        {
            var longPath = new WeaponPath(new[] { new Vec2(PathStartX, ShoulderHeight), new Vec2(PathStartX, 2000f) });

            SwingResult result = Run(longPath);

            Assert.That(result.EndReason, Is.EqualTo(SwingEndReason.HardCap));
            Assert.That(result.StepsSimulated, Is.EqualTo(_clock.StepsFor(_rules.Match.ExecutionHardCapSeconds)));
        }

        [Test]
        public void SideWithoutAPath_HoldsItsRootAndWeapon()
        {
            SwingResult result = Run(StraightThrust);

            Assert.That(result.FinalPose.Right.Get(BodyPart.Torso).PositionUnits, Is.EqualTo(new Vec2(1000f, 0f)));
            Assert.That(result.FinalPose.Right.HeldItem.PositionUnits, Is.EqualTo(new Vec2(1000f, ShoulderHeight)));
        }

        [Test]
        public void MotorDriver_FollowsThePathToItsEnd()
        {
            _simulation.WeaponDriver = WeaponDriverKind.Motor;

            SwingResult result = Run(StraightThrust);

            Assert.That(Vec2.Distance(Tip(result.FinalPose.Left.HeldItem), StraightThrust.Points[1]), Is.LessThan(20f));
        }

        [Test]
        public void Recording_KeepsEveryNthStepAndTheLast()
        {
            _simulation.RecordEveryNthStep = 4;

            SwingResult result = Run(StraightThrust);

            TurnTimeline timeline = result.Timeline;
            Assert.That(timeline.Frames[0].Step, Is.EqualTo(0));
            Assert.That(timeline.Frames[1].Step, Is.EqualTo(4));
            Assert.That(timeline.Frames[timeline.Frames.Count - 1].Step, Is.EqualTo(result.StepsSimulated));
        }

        [Test]
        public void World_IsLoadedOnceWithTheSimulationSettings()
        {
            Run(StraightThrust);

            Assert.That(_world.LoadCount, Is.EqualTo(1));
            Assert.That(_world.LoadedSimulation, Is.SameAs(_simulation));
        }
    }
}
