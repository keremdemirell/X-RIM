using NUnit.Framework;
using XRim.Core;
using XRim.Rules.Combat;

namespace XRim.Tests.EditMode.Rules.Combat
{
    /// <summary>GDD §10 stage 1: the angle between relative motion and the contact surface; 0° slides, 90° is square.</summary>
    public sealed class ContactAngleTests
    {
        private const float Tolerance = 1e-3f;
        private static readonly Vec2 Up = Vec2.UnitY;

        [Test]
        public void MotionIntoTheSurface_IsASquareImpact()
        {
            Assert.That(ContactAngle.Degrees(Up, new Vec2(0f, -900f)), Is.EqualTo(90f).Within(Tolerance));
        }

        [Test]
        public void MotionAlongTheSurface_IsSliding()
        {
            Assert.That(ContactAngle.Degrees(Up, new Vec2(900f, 0f)), Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void DiagonalMotion_MeetsAt45Degrees()
        {
            Assert.That(ContactAngle.Degrees(Up, new Vec2(250f, -250f)), Is.EqualTo(45f).Within(Tolerance));
        }

        [Test]
        public void IsTheSameForBothSides()
        {
            var normal = new Vec2(0.6f, 0.8f);
            var relative = new Vec2(-300f, 120f);

            Assert.That(ContactAngle.Degrees(-normal, -relative), Is.EqualTo(ContactAngle.Degrees(normal, relative)).Within(Tolerance));
        }

        [Test]
        public void DoesNotDependOnSpeedOrNormalLength()
        {
            Assert.That(ContactAngle.Degrees(Up * 5f, new Vec2(10f, -10f)),
                Is.EqualTo(ContactAngle.Degrees(Up, new Vec2(1000f, -1000f))).Within(Tolerance));
        }

        [Test]
        public void NoRelativeMotion_CountsAsSliding()
        {
            Assert.That(ContactAngle.Degrees(Up, Vec2.Zero), Is.EqualTo(0f));
        }
    }
}
