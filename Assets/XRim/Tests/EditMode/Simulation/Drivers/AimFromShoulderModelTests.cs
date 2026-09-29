using NUnit.Framework;
using XRim.Core;
using XRim.Simulation.Drivers;
using XRim.Simulation.Physics;

namespace XRim.Tests.EditMode.Simulation.Drivers
{
    /// <summary>Designer, 2026-09-29: the weapon lies on the line from the shoulder through the path point.</summary>
    public sealed class AimFromShoulderModelTests
    {
        private const float Tolerance = 1e-3f;
        private const float ArmLength = 240f;
        private const float RapierLength = 400f;
        private static readonly Vec2 Shoulder = new Vec2(0f, 100f);

        private readonly AimFromShoulderModel _model = new AimFromShoulderModel();

        private static Vec2 Tip(BodyPose grip, float length) => grip.PositionUnits + Vec2.FromAngleDegrees(grip.RotationDegrees) * length;

        private static void AssertNear(Vec2 actual, Vec2 expected)
        {
            Assert.That(actual.X, Is.EqualTo(expected.X).Within(Tolerance), "x");
            Assert.That(actual.Y, Is.EqualTo(expected.Y).Within(Tolerance), "y");
        }

        [Test]
        public void FullReach_ExtendsTheArmAlongTheLine()
        {
            BodyPose grip = _model.Aim(new Vec2(640f, 100f), Shoulder, ArmLength, RapierLength);

            AssertNear(grip.PositionUnits, new Vec2(240f, 100f));
            Assert.That(grip.RotationDegrees, Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void OverheadPoint_PointsTheWeaponUp()
        {
            BodyPose grip = _model.Aim(new Vec2(0f, 600f), Shoulder, ArmLength, RapierLength);

            AssertNear(grip.PositionUnits, new Vec2(0f, 200f));
            Assert.That(grip.RotationDegrees, Is.EqualTo(90f).Within(Tolerance));
        }

        [TestCase(300f, 100f)]
        [TestCase(200f, 350f)]
        [TestCase(-150f, 300f)]
        public void ReachablePoint_TipLandsExactlyOnIt(float x, float y)
        {
            var point = new Vec2(x, y);

            AssertNear(Tip(_model.Aim(point, Shoulder, ArmLength, RapierLength), RapierLength), point);
        }

        [Test]
        public void PointInsideTheWeaponsLength_PullsTheHandBehindTheShoulder()
        {
            BodyPose grip = _model.Aim(new Vec2(300f, 100f), Shoulder, ArmLength, RapierLength);

            AssertNear(grip.PositionUnits, new Vec2(-100f, 100f));
        }

        [Test]
        public void PointTooCloseToTheShoulder_StillPointsThroughIt()
        {
            BodyPose grip = _model.Aim(new Vec2(100f, 100f), Shoulder, ArmLength, RapierLength);

            AssertNear(grip.PositionUnits, new Vec2(-ArmLength, 100f));
            Assert.That(grip.RotationDegrees, Is.EqualTo(0f).Within(Tolerance));
            AssertNear(Tip(grip, RapierLength), new Vec2(160f, 100f));
        }

        [Test]
        public void PointOnTheShoulder_PointsForward()
        {
            BodyPose grip = _model.Aim(Shoulder, Shoulder, ArmLength, RapierLength);

            Assert.That(grip.RotationDegrees, Is.EqualTo(0f).Within(Tolerance));
        }
    }
}
