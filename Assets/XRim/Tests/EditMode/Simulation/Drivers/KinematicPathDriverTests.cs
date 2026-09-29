using NUnit.Framework;
using XRim.Core;
using XRim.Rules.Paths;
using XRim.Rules.Settings;
using XRim.Simulation.Drivers;
using XRim.Simulation.Physics;

namespace XRim.Tests.EditMode.Simulation.Drivers
{
    /// <summary>
    /// GDD §9 (Decided): the tip is at d = v·t with the weapon's own speed. With the default settings the shoulder is
    /// at (0, 100) in the torso frame, so a path along y = 100 runs straight out from it.
    /// </summary>
    public sealed class KinematicPathDriverTests
    {
        private const float Tolerance = 1e-2f;
        private static readonly BodyPose TorsoAtOrigin = new BodyPose(Vec2.Zero, 0f);
        private static readonly WeaponPath StraightThrust = new WeaponPath(new[] { new Vec2(400f, 100f), new Vec2(640f, 100f) });

        private readonly PathSettings _paths = new PathSettings();
        private readonly WeaponStats _rapier = GddStartingValues.Rapier();

        private KinematicPathDriver Begin(Side side, WeaponPath path, WeaponStats weapon)
        {
            var driver = new KinematicPathDriver(_paths, new AimFromShoulderModel());
            driver.Begin(side, path, weapon);
            return driver;
        }

        private static Vec2 Tip(BodyPose grip, WeaponStats weapon) =>
            grip.PositionUnits + Vec2.FromAngleDegrees(grip.RotationDegrees) * weapon.LengthUnits;

        private static void AssertNear(Vec2 actual, Vec2 expected)
        {
            Assert.That(actual.X, Is.EqualTo(expected.X).Within(Tolerance), "x");
            Assert.That(actual.Y, Is.EqualTo(expected.Y).Within(Tolerance), "y");
        }

        [Test]
        public void Tip_IsAtWeaponSpeedTimesTime()
        {
            KinematicPathDriver driver = Begin(Side.Left, StraightThrust, _rapier);
            SimTime time = SimTime.FromSeconds(0.1);

            float expected = _rapier.SpeedUnitsPerSecond * 0.1f;
            Assert.That(driver.DistanceAlongPathUnits(time), Is.EqualTo(expected).Within(Tolerance));
            AssertNear(Tip(driver.EvaluateTarget(time, TorsoAtOrigin), _rapier), new Vec2(400f + expected, 100f));
        }

        [Test]
        public void SlowerWeapon_IsBehindAtTheSameTime()
        {
            SimTime time = SimTime.FromSeconds(0.1);
            WeaponStats mace = GddStartingValues.Mace();

            Assert.That(Begin(Side.Left, StraightThrust, mace).DistanceAlongPathUnits(time),
                Is.LessThan(Begin(Side.Left, StraightThrust, _rapier).DistanceAlongPathUnits(time)));
        }

        [Test]
        public void Swing_CompletesAtLengthOverSpeed()
        {
            KinematicPathDriver driver = Begin(Side.Left, StraightThrust, _rapier);
            double finishSeconds = StraightThrust.LengthUnits / _rapier.SpeedUnitsPerSecond;

            Assert.That(driver.IsComplete(SimTime.FromSeconds(finishSeconds - 0.001)), Is.False);
            Assert.That(driver.IsComplete(SimTime.FromSeconds(finishSeconds)), Is.True);
            Assert.That(driver.DistanceAlongPathUnits(SimTime.FromSeconds(10.0)), Is.EqualTo(StraightThrust.LengthUnits).Within(Tolerance));
        }

        [Test]
        public void Drive_MovesOntoTheTargetAtTheEndOfTheStep()
        {
            KinematicPathDriver driver = Begin(Side.Left, StraightThrust, _rapier);
            var stepStart = new SimTime(0L);
            var stepEnd = new SimTime(4_166L);

            HeldItemCommand command = driver.Drive(stepStart, stepEnd, TorsoAtOrigin, default);

            Assert.That(command.Kind, Is.EqualTo(HeldItemCommandKind.MoveTo));
            AssertNear(command.Target.PositionUnits, driver.EvaluateTarget(stepEnd, TorsoAtOrigin).PositionUnits);
        }

        [Test]
        public void RightFighter_ThrustsTowardMinusX()
        {
            KinematicPathDriver driver = Begin(Side.Right, StraightThrust, _rapier);
            var torso = new BodyPose(new Vec2(1000f, 0f), 0f);

            BodyPose grip = driver.EvaluateTarget(SimTime.Zero, torso);

            AssertNear(Tip(grip, _rapier), new Vec2(600f, 100f));
            Assert.That(grip.RotationDegrees, Is.EqualTo(180f).Within(Tolerance));
        }

        [Test]
        public void Target_MovesWithTheTorso()
        {
            KinematicPathDriver driver = Begin(Side.Left, StraightThrust, _rapier);
            SimTime time = SimTime.FromSeconds(0.05);
            var moved = new BodyPose(new Vec2(50f, -20f), 0f);

            Vec2 shift = driver.EvaluateTarget(time, moved).PositionUnits - driver.EvaluateTarget(time, TorsoAtOrigin).PositionUnits;

            AssertNear(shift, new Vec2(50f, -20f));
        }

        [Test]
        public void Cancel_LetsTheWeaponGoLimp()
        {
            KinematicPathDriver driver = Begin(Side.Left, StraightThrust, _rapier);

            driver.Cancel();

            Assert.That(driver.IsComplete(SimTime.Zero), Is.True);
            HeldItemCommand command = driver.Drive(SimTime.Zero, new SimTime(4_166L), TorsoAtOrigin, default);
            Assert.That(command.Kind, Is.EqualTo(HeldItemCommandKind.Push));
            Assert.That(command.AccelerationUnitsPerSecondSquared, Is.EqualTo(Vec2.Zero));
        }

        [Test]
        public void NoPath_HoldsTheWeaponWhereItIs()
        {
            KinematicPathDriver driver = Begin(Side.Left, WeaponPath.Empty, _rapier);
            var held = new BodyState(new BodyPose(new Vec2(120f, 80f), 30f), Vec2.Zero, 0f);

            HeldItemCommand command = driver.Drive(SimTime.Zero, new SimTime(4_166L), TorsoAtOrigin, held);

            Assert.That(driver.IsComplete(SimTime.Zero), Is.True);
            Assert.That(command.Kind, Is.EqualTo(HeldItemCommandKind.MoveTo));
            Assert.That(command.Target.PositionUnits, Is.EqualTo(held.Pose.PositionUnits));
            Assert.That(command.Target.RotationDegrees, Is.EqualTo(held.Pose.RotationDegrees));
        }
    }
}
