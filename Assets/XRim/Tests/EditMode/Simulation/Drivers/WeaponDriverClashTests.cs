using NUnit.Framework;
using XRim.Core;
using XRim.Rules.Paths;
using XRim.Rules.Settings;
using XRim.Simulation;
using XRim.Simulation.Drivers;
using XRim.Simulation.Physics;
using XRim.Simulation.Settings;

namespace XRim.Tests.EditMode.Simulation.Drivers
{
    /// <summary>
    /// What the clash and block rules do to a weapon on its path (GDD §7, §10; A6): a rebound bounces it back, a knock-off kicks
    /// it away and lets it fly free before the hand takes it where it ended up. The rapier thrusts along y = 100 straight out
    /// from the shoulder at 900 units/s, so its grip moves with its tip.
    /// </summary>
    public sealed class WeaponDriverClashTests
    {
        private const float Tolerance = 1e-2f;
        private static readonly BodyPose TorsoAtOrigin = new BodyPose(Vec2.Zero, 0f);
        private static readonly WeaponPath StraightThrust = new WeaponPath(new[] { new Vec2(400f, 100f), new Vec2(640f, 100f) });

        private readonly PathSettings _paths = new PathSettings();
        private readonly WeaponStats _rapier = GddStartingValues.Rapier();
        private readonly SimClock _clock = new SimClock(new SimulationSettings().StepRateHz);
        private HitReactionSettings _reaction;
        private ClashReactionSettings _clash;

        [SetUp]
        public void SetUp()
        {
            _reaction = new HitReactionSettings();
            _clash = new ClashReactionSettings();
        }

        private KinematicPathDriver Kinematic()
        {
            var driver = new KinematicPathDriver(_paths, new AimFromShoulderModel(), new WeaponMotorSettings(), _reaction, _clash);
            driver.Begin(Side.Left, StraightThrust, _rapier);
            return driver;
        }

        private HeldItemCommand DriveStep(IWeaponDriver driver, int step, BodyState heldItem = default) =>
            driver.Drive(_clock.TimeAtStep(step), _clock.TimeAtStep(step + 1), TorsoAtOrigin, TorsoAtOrigin, heldItem);

        /// <summary>Kinematic steps, so the driver knows how fast the blade moves; then the stop at the start of the next step.</summary>
        private KinematicPathDriver StoppedAfterSteps(WeaponStopKind kind, Vec2 knock = default, int steps = 2)
        {
            KinematicPathDriver driver = Kinematic();
            for (int step = 0; step < steps; step++) DriveStep(driver, step);
            driver.Stop(_clock.TimeAtStep(steps), kind, knock);
            return driver;
        }

        private Vec2 Tip(BodyPose grip) => grip.PositionUnits + Vec2.FromAngleDegrees(grip.RotationDegrees) * _rapier.LengthUnits;

        [Test]
        public void ARebound_BouncesTheBladeBack_AtHalfItsSpeed_ThenHoldsItBackedOffByTheRecoil()
        {
            const int steps = 20;
            KinematicPathDriver driver = StoppedAfterSteps(WeaponStopKind.Rebounded, steps: steps);

            HeldItemCommand release = DriveStep(driver, steps);

            Assert.That(release.Kind, Is.EqualTo(HeldItemCommandKind.Release));
            Assert.That(release.VelocityUnitsPerSecond.X, Is.EqualTo(-_rapier.SpeedUnitsPerSecond * 0.5f).Within(1f), "back the way it came");
            Assert.That(DriveStep(driver, steps + 1).Kind, Is.EqualTo(HeldItemCommandKind.Push), "then the motor holds it");
            float stopDistance = (float)(_rapier.SpeedUnitsPerSecond * _clock.TimeAtStep(steps).Seconds);
            Assert.That(Tip(driver.EvaluateTarget(_clock.TimeAtStep(steps + 10), TorsoAtOrigin)).X,
                Is.EqualTo(400f + stopDistance - _reaction.RecoilDistanceUnits).Within(Tolerance));
        }

        [Test]
        public void ARebound_FollowsItsTunedSpeed()
        {
            _clash.ReboundSpeedFraction = 0.2f;

            HeldItemCommand release = DriveStep(StoppedAfterSteps(WeaponStopKind.Rebounded), 2);

            Assert.That(release.VelocityUnitsPerSecond.X, Is.EqualTo(-_rapier.SpeedUnitsPerSecond * 0.2f).Within(1f));
        }

        [Test]
        public void AHeadOnKnockOff_StopsTheBladesOwnMotion_AndSwatsItBack()
        {
            HeldItemCommand release = DriveStep(StoppedAfterSteps(WeaponStopKind.KnockedOff, new Vec2(-625f, 0f)), 2);

            Assert.That(release.Kind, Is.EqualTo(HeldItemCommandKind.Release));
            Assert.That(release.VelocityUnitsPerSecond.X, Is.EqualTo(-625f).Within(1f), "its forward motion along the knock is gone");
            Assert.That(release.VelocityUnitsPerSecond.Y, Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void ASidewaysKnockOff_KeepsTheBladesForwardMotion_AndAddsTheKick()
        {
            HeldItemCommand release = DriveStep(StoppedAfterSteps(WeaponStopKind.KnockedOff, new Vec2(0f, 90f)), 2);

            Assert.That(release.VelocityUnitsPerSecond.X, Is.EqualTo(_rapier.SpeedUnitsPerSecond).Within(1f), "redirected, not stopped");
            Assert.That(release.VelocityUnitsPerSecond.Y, Is.EqualTo(90f).Within(Tolerance));
        }

        [Test]
        public void AKnockedOffBlade_FliesFree_ThenIsHeldWhereItEndedUp()
        {
            KinematicPathDriver driver = StoppedAfterSteps(WeaponStopKind.KnockedOff, new Vec2(-625f, 0f));
            DriveStep(driver, 2);
            int freeSteps = _clock.StepsFor(_clash.KnockOffFreeSeconds);
            var flown = new BodyState(new BodyPose(new Vec2(120f, 60f), -30f), Vec2.Zero, 0f);

            HeldItemCommand flying = DriveStep(driver, 3, flown);
            HeldItemCommand caught = DriveStep(driver, 2 + freeSteps, flown);
            HeldItemCommand held = DriveStep(driver, 3 + freeSteps, flown);

            Assert.That(flying.Kind, Is.EqualTo(HeldItemCommandKind.Push));
            Assert.That(flying.AccelerationUnitsPerSecondSquared, Is.EqualTo(Vec2.Zero), "limp while it flies free");
            Assert.That(caught.Kind, Is.EqualTo(HeldItemCommandKind.Push));
            Assert.That(held.AccelerationUnitsPerSecondSquared.Length, Is.LessThan(Tolerance), "the hand holds it right where it is");
            Assert.That(driver.EvaluateTarget(_clock.TimeAtStep(100), TorsoAtOrigin).PositionUnits, Is.EqualTo(flown.Pose.PositionUnits),
                "off its path");
        }

        [Test]
        public void AFreeBlade_ReboundsAtItsOwnVelocity()
        {
            var driver = new MotorPathDriver(_paths, new WeaponMotorSettings(), new AimFromShoulderModel(), _reaction, _clash);
            driver.Begin(Side.Left, StraightThrust, _rapier);
            var moving = new BodyState(new BodyPose(new Vec2(160f, 100f), 0f), new Vec2(300f, 40f), 10f);
            DriveStep(driver, 0, moving);

            driver.Stop(_clock.TimeAtStep(1), WeaponStopKind.Rebounded);
            HeldItemCommand release = DriveStep(driver, 1, moving);

            Assert.That(release.Kind, Is.EqualTo(HeldItemCommandKind.Release), "a motor blade is already free, but it still bounces");
            Assert.That(release.VelocityUnitsPerSecond.X, Is.EqualTo(-150f).Within(Tolerance));
            Assert.That(release.VelocityUnitsPerSecond.Y, Is.EqualTo(-20f).Within(Tolerance));
            Assert.That(release.AngularVelocityDegreesPerSecond, Is.EqualTo(-5f).Within(Tolerance));
        }

        [Test]
        public void AnInterruptedFreeBlade_IsStillNotReleased()
        {
            var driver = new MotorPathDriver(_paths, new WeaponMotorSettings(), new AimFromShoulderModel(), _reaction, _clash);
            driver.Begin(Side.Left, StraightThrust, _rapier);
            DriveStep(driver, 0);

            driver.Stop(_clock.TimeAtStep(1), WeaponStopKind.Interrupted);

            Assert.That(DriveStep(driver, 1).Kind, Is.EqualTo(HeldItemCommandKind.Push), "Session 06 behaviour: the motor just holds it");
        }
    }
}
