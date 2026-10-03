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
    /// What the hit rules do to a weapon on its path: a slowing hit (D26), a stop, the hand-off to physics (D1) and the recoil.
    /// The thrust runs along y = 100 straight out from the shoulder, so the blade lies flat and moves with its tip.
    /// </summary>
    public sealed class WeaponDriverHitTests
    {
        private const float Tolerance = 1e-2f;
        private static readonly BodyPose TorsoAtOrigin = new BodyPose(Vec2.Zero, 0f);
        private static readonly WeaponPath StraightThrust = new WeaponPath(new[] { new Vec2(400f, 100f), new Vec2(640f, 100f) });

        private readonly PathSettings _paths = new PathSettings();
        private readonly WeaponStats _rapier = GddStartingValues.Rapier();
        private readonly SimClock _clock = new SimClock(new SimulationSettings().StepRateHz);
        private HitReactionSettings _reaction;

        [SetUp]
        public void SetUp() => _reaction = new HitReactionSettings();

        private KinematicPathDriver Kinematic(WeaponPath path = null)
        {
            var driver = new KinematicPathDriver(_paths, new AimFromShoulderModel(), new WeaponMotorSettings(), _reaction);
            driver.Begin(Side.Left, path ?? StraightThrust, _rapier);
            return driver;
        }

        private Vec2 Tip(BodyPose grip) => grip.PositionUnits + Vec2.FromAngleDegrees(grip.RotationDegrees) * _rapier.LengthUnits;

        private HeldItemCommand DriveStep(IWeaponDriver driver, int step) =>
            driver.Drive(_clock.TimeAtStep(step), _clock.TimeAtStep(step + 1), TorsoAtOrigin, TorsoAtOrigin, default);

        [Test]
        public void SlowTo_ContinuesTheSameWay_AtAShareOfItsSpeed()
        {
            KinematicPathDriver driver = Kinematic();

            driver.SlowTo(SimTime.FromSeconds(0.1), 0.5f);

            float atHit = _rapier.SpeedUnitsPerSecond * 0.1f;
            Assert.That(driver.DistanceAlongPathUnits(SimTime.FromSeconds(0.05)), Is.EqualTo(_rapier.SpeedUnitsPerSecond * 0.05f).Within(Tolerance),
                "before the hit: unchanged");
            Assert.That(driver.DistanceAlongPathUnits(SimTime.FromSeconds(0.2)),
                Is.EqualTo(atHit + _rapier.SpeedUnitsPerSecond * 0.5f * 0.1f).Within(Tolerance));
        }

        [Test]
        public void ASlowedWeapon_TravelsItsPathForLonger()
        {
            KinematicPathDriver driver = Kinematic();
            SimTime fullSpeedEnd = SimTime.FromSeconds(StraightThrust.LengthUnits / _rapier.SpeedUnitsPerSecond);

            driver.SlowTo(SimTime.FromSeconds(0.1), 0.3f);

            Assert.That(driver.IsTravelling(fullSpeedEnd), Is.True);
            Assert.That(driver.IsComplete(fullSpeedEnd), Is.False);
            Assert.That(driver.IsTravelling(SimTime.FromSeconds(10.0)), Is.False, "it still reaches the end");
        }

        [Test]
        public void Stop_FreezesTheDistance_AndEndsTheAttackFromThatTime()
        {
            KinematicPathDriver driver = Kinematic();
            SimTime stop = SimTime.FromSeconds(0.1);

            driver.Stop(stop, WeaponStopKind.Interrupted);

            Assert.That(driver.DistanceAlongPathUnits(SimTime.FromSeconds(0.2)), Is.EqualTo(_rapier.SpeedUnitsPerSecond * 0.1f).Within(Tolerance));
            Assert.That(driver.IsTravelling(SimTime.FromSeconds(0.05)), Is.True, "it was travelling before the stop");
            Assert.That(driver.IsTravelling(stop), Is.False);
            Assert.That(driver.IsComplete(stop), Is.True);
            driver.SlowTo(SimTime.FromSeconds(0.15), 0.5f);
            Assert.That(driver.DistanceAlongPathUnits(SimTime.FromSeconds(0.2)), Is.EqualTo(_rapier.SpeedUnitsPerSecond * 0.1f).Within(Tolerance),
                "a stopped weapon stays stopped");
        }

        [Test]
        public void IsTravelling_NeedsAPath_AndEndsWithIt()
        {
            Assert.That(Kinematic(WeaponPath.Empty).IsTravelling(SimTime.Zero), Is.False, "a held weapon without a path rests (E2)");
            Assert.That(Kinematic().IsTravelling(SimTime.Zero), Is.True);
            Assert.That(Kinematic().IsTravelling(SimTime.FromSeconds(1.0)), Is.False, "its path is over");
        }

        [Test]
        public void AfterAStop_AKinematicBlade_IsHandedToPhysicsWithItsSpeed_ThenHeldByTheMotor()
        {
            KinematicPathDriver driver = Kinematic();
            DriveStep(driver, 0);
            DriveStep(driver, 1);

            driver.Stop(_clock.TimeAtStep(2), WeaponStopKind.Interrupted);
            HeldItemCommand release = DriveStep(driver, 2);
            HeldItemCommand hold = DriveStep(driver, 3);

            Assert.That(release.Kind, Is.EqualTo(HeldItemCommandKind.Release));
            Assert.That(release.VelocityUnitsPerSecond.X, Is.EqualTo(_rapier.SpeedUnitsPerSecond).Within(1f), "D1: its own speed");
            Assert.That(release.VelocityUnitsPerSecond.Y, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(hold.Kind, Is.EqualTo(HeldItemCommandKind.Push), "then the motor keeps it in the hand");
        }

        [Test]
        public void TheHandOff_CarriesTheTunedShareOfItsSpeed()
        {
            _reaction.ReleaseSpeedFraction = 0.25f;
            KinematicPathDriver driver = Kinematic();
            DriveStep(driver, 0);
            DriveStep(driver, 1);

            driver.Stop(_clock.TimeAtStep(2), WeaponStopKind.LastHit);

            Assert.That(DriveStep(driver, 2).VelocityUnitsPerSecond.X, Is.EqualTo(_rapier.SpeedUnitsPerSecond * 0.25f).Within(1f));
        }

        [Test]
        public void ALastHit_HoldsTheWeaponBackAlongItsPath_ByTheRecoil()
        {
            KinematicPathDriver driver = Kinematic();
            SimTime stop = SimTime.FromSeconds(0.1);
            float distance = _rapier.SpeedUnitsPerSecond * 0.1f;

            driver.Stop(stop, WeaponStopKind.LastHit);

            Vec2 tip = Tip(driver.EvaluateTarget(SimTime.FromSeconds(0.2), TorsoAtOrigin));
            Assert.That(tip.X, Is.EqualTo(400f + distance - _reaction.RecoilDistanceUnits).Within(Tolerance));
        }

        [Test]
        public void AnInterruptedWeapon_IsHeldWhereItStopped()
        {
            KinematicPathDriver driver = Kinematic();

            driver.Stop(SimTime.FromSeconds(0.1), WeaponStopKind.Interrupted);

            Vec2 tip = Tip(driver.EvaluateTarget(SimTime.FromSeconds(0.2), TorsoAtOrigin));
            Assert.That(tip.X, Is.EqualTo(400f + _rapier.SpeedUnitsPerSecond * 0.1f).Within(Tolerance));
        }

        [Test]
        public void TheHoldPoint_TravelsWithTheTorso()
        {
            KinematicPathDriver driver = Kinematic();
            driver.Stop(SimTime.FromSeconds(0.1), WeaponStopKind.Interrupted);
            var moved = new BodyPose(new Vec2(-40f, 0f), 0f);

            Vec2 shift = driver.EvaluateTarget(SimTime.FromSeconds(0.2), moved).PositionUnits -
                         driver.EvaluateTarget(SimTime.FromSeconds(0.2), TorsoAtOrigin).PositionUnits;

            Assert.That(shift.X, Is.EqualTo(-40f).Within(Tolerance), "knocked back, the weapon goes with the dummy");
        }

        [Test]
        public void AMotorDriversBlade_IsAlreadyFree_SoAStopNeedsNoHandOff()
        {
            var driver = new MotorPathDriver(_paths, new WeaponMotorSettings(), new AimFromShoulderModel(), _reaction);
            driver.Begin(Side.Left, StraightThrust, _rapier);
            DriveStep(driver, 0);

            driver.Stop(_clock.TimeAtStep(1), WeaponStopKind.LastHit);

            Assert.That(DriveStep(driver, 1).Kind, Is.EqualTo(HeldItemCommandKind.Push));
        }
    }
}
