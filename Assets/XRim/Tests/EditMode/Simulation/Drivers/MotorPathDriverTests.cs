using System;
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
    /// <summary>The motor chases the same d = v·t target as the kinematic driver, with a mass-free spring-damper.</summary>
    public sealed class MotorPathDriverTests
    {
        private const float Tolerance = 1e-2f;
        private static readonly BodyPose TorsoAtOrigin = new BodyPose(Vec2.Zero, 0f);
        private static readonly WeaponPath StraightThrust = new WeaponPath(new[] { new Vec2(400f, 100f), new Vec2(640f, 100f) });

        private readonly PathSettings _paths = new PathSettings();
        private readonly WeaponStats _rapier = GddStartingValues.Rapier();
        private readonly SimClock _clock = new SimClock(new SimulationSettings().StepRateHz);

        private MotorPathDriver Begin(WeaponMotorSettings motor)
        {
            var driver = new MotorPathDriver(_paths, motor, new AimFromShoulderModel());
            driver.Begin(Side.Left, StraightThrust, _rapier);
            return driver;
        }

        [Test]
        public void OnTargetAndAtPathSpeed_NeedsNoPush()
        {
            MotorPathDriver driver = Begin(new WeaponMotorSettings());
            SimTime start = _clock.TimeAtStep(10);
            SimTime end = _clock.TimeAtStep(11);
            BodyPose target = driver.EvaluateTarget(start, TorsoAtOrigin);
            var held = new BodyState(target, new Vec2(_rapier.SpeedUnitsPerSecond, 0f), 0f);

            HeldItemCommand command = driver.Drive(start, end, TorsoAtOrigin, held);

            Assert.That(command.Kind, Is.EqualTo(HeldItemCommandKind.Push));
            Assert.That(command.AccelerationUnitsPerSecondSquared.Length, Is.LessThan(1f));
            Assert.That(Math.Abs(command.AngularAccelerationDegreesPerSecondSquared), Is.LessThan(Tolerance));
        }

        [Test]
        public void BehindTheTarget_PushesTowardIt()
        {
            MotorPathDriver driver = Begin(new WeaponMotorSettings());
            BodyPose target = driver.EvaluateTarget(SimTime.Zero, TorsoAtOrigin);
            var held = new BodyState(new BodyPose(target.PositionUnits - new Vec2(20f, 0f), 0f), Vec2.Zero, 0f);

            HeldItemCommand command = driver.Drive(SimTime.Zero, _clock.TimeAtStep(1), TorsoAtOrigin, held);

            Assert.That(command.AccelerationUnitsPerSecondSquared.X, Is.GreaterThan(0f));
        }

        [Test]
        public void Push_IsLimitedByTheMotorsStrength()
        {
            var motor = new WeaponMotorSettings { MaxAccelerationUnitsPerSecondSquared = 1000f };
            MotorPathDriver driver = Begin(motor);
            var held = new BodyState(new BodyPose(new Vec2(-500f, -500f), 0f), Vec2.Zero, 0f);

            HeldItemCommand command = driver.Drive(SimTime.Zero, _clock.TimeAtStep(1), TorsoAtOrigin, held);

            Assert.That(command.AccelerationUnitsPerSecondSquared.Length, Is.EqualTo(1000f).Within(Tolerance));
        }

        [Test]
        public void FreeFlyingWeapon_TracksThePathAndArrivesOnTime()
        {
            MotorPathDriver driver = Begin(new WeaponMotorSettings());
            BodyPose start = driver.EvaluateTarget(SimTime.Zero, TorsoAtOrigin);
            Vec2 position = start.PositionUnits;
            Vec2 velocity = Vec2.Zero;
            float rotation = start.RotationDegrees;
            float angularVelocity = 0f;
            float maxLag = 0f;
            double finishSeconds = StraightThrust.LengthUnits / _rapier.SpeedUnitsPerSecond;
            int finishStep = _clock.StepsFor(finishSeconds);
            int settledStep = _clock.StepsFor(finishSeconds + 0.3);

            for (int step = 0; step < settledStep; step++)
            {
                SimTime stepEnd = _clock.TimeAtStep(step + 1);
                var held = new BodyState(new BodyPose(position, rotation), velocity, angularVelocity);
                HeldItemCommand command = driver.Drive(_clock.TimeAtStep(step), stepEnd, TorsoAtOrigin, held);

                // Semi-implicit Euler, as Box2D integrates bodies.
                velocity += command.AccelerationUnitsPerSecondSquared * _clock.StepSeconds;
                position += velocity * _clock.StepSeconds;
                angularVelocity += command.AngularAccelerationDegreesPerSecondSquared * _clock.StepSeconds;
                rotation += angularVelocity * _clock.StepSeconds;
                if (step < finishStep) maxLag = Math.Max(maxLag, Vec2.Distance(position, driver.EvaluateTarget(stepEnd, TorsoAtOrigin).PositionUnits));
            }

            BodyPose end = driver.EvaluateTarget(_clock.TimeAtStep(settledStep), TorsoAtOrigin);
            Assert.That(Vec2.Distance(position, end.PositionUnits), Is.LessThan(1f), "settles on the path's end");
            Assert.That(maxLag, Is.LessThan(20f), "stays close behind the d = v·t target while moving");
        }
    }
}
