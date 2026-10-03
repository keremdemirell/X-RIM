using NUnit.Framework;
using XRim.Core;
using XRim.Rules.Settings;
using XRim.Simulation.Drivers;
using XRim.Simulation.Execution;
using XRim.Simulation.Physics;

namespace XRim.Tests.EditMode.Simulation.Drivers
{
    /// <summary>
    /// The shield held like a shield (designer, 2026-10-03, A3): the drawn point is its centre, the hand reaches toward it, and
    /// the face turns outward, square to the arm. Shoulder at (0, 100), arm 240, shield 150 tall and 40 thick.
    /// </summary>
    public sealed class ShieldFaceAimModelTests
    {
        private const float Tolerance = 1e-3f;
        private const float ArmLength = 240f;
        private static readonly Vec2 Shoulder = new Vec2(0f, 100f);

        private readonly ShieldFaceAimModel _aim = new ShieldFaceAimModel();
        private readonly WeaponStats _shield = GddStartingValues.Shield();

        private BodyPose Aim(Vec2 point) => _aim.Aim(point, Shoulder, ArmLength, _shield.LengthUnits);

        private static void AssertNear(Vec2 actual, Vec2 expected, string what)
        {
            Assert.That(actual.X, Is.EqualTo(expected.X).Within(Tolerance), what + " x");
            Assert.That(actual.Y, Is.EqualTo(expected.Y).Within(Tolerance), what + " y");
        }

        [Test]
        public void AReachablePoint_IsWhereTheShieldsCentreGoes_FacingOutFromTheShoulder()
        {
            BodyPose grip = Aim(new Vec2(150f, 100f));

            AssertNear(grip.PositionUnits, new Vec2(150f, 100f), "centre on the drawn point");
            Assert.That(grip.RotationDegrees, Is.EqualTo(0f).Within(Tolerance), "the face looks at the opponent");
        }

        [Test]
        public void APointAboveTheShoulder_RaisesTheShieldOverhead_FacingUp()
        {
            BodyPose grip = Aim(new Vec2(0f, 250f));

            AssertNear(grip.PositionUnits, new Vec2(0f, 250f), "centre");
            Assert.That(grip.RotationDegrees, Is.EqualTo(90f).Within(Tolerance));
        }

        [Test]
        public void APointBeyondTheArm_HoldsTheShieldAtFullStretch_OnTheSameLine()
        {
            BodyPose grip = Aim(new Vec2(500f, 100f));

            AssertNear(grip.PositionUnits, new Vec2(ArmLength, 100f), "centre at arm's length");
            Assert.That(grip.RotationDegrees, Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void APointOnTheShoulder_HoldsItThere_FacingTheOpponent()
        {
            BodyPose grip = Aim(Shoulder);

            AssertNear(grip.PositionUnits, Shoulder, "centre");
            Assert.That(grip.RotationDegrees, Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void TheShieldsLength_GivesItNoExtraReach()
        {
            BodyPose shortShield = _aim.Aim(new Vec2(500f, 100f), Shoulder, ArmLength, 10f);

            Assert.That(shortShield.PositionUnits, Is.EqualTo(Aim(new Vec2(500f, 100f)).PositionUnits));
            Assert.That(_shield.ReachBeyondHandUnits, Is.EqualTo(0f));
        }

        [Test]
        public void TheFace_StandsSquareToTheArm_CoveringItsWholeHeight()
        {
            var point = new Vec2(120f, 160f);
            BodyPose grip = Aim(point);
            BladeShape face = BladeShape.Of(_shield);
            Vec2 arm = (grip.PositionUnits - Shoulder).Normalized;
            var across = new Vec2(-arm.Y, arm.X);
            float halfHeight = _shield.LengthUnits * 0.5f;

            Assert.That(face.DistanceToCentreLine(point + across * halfHeight, grip), Is.EqualTo(0f).Within(Tolerance), "one end of the face");
            Assert.That(face.DistanceToCentreLine(point - across * halfHeight, grip), Is.EqualTo(0f).Within(Tolerance), "the other end");
            Assert.That(face.DistanceToCentreLine(point + arm * 50f, grip), Is.EqualTo(50f).Within(Tolerance), "50 in front of the face");
        }
    }
}
