using NUnit.Framework;
using XRim.Core;

namespace XRim.Tests.EditMode.Core
{
    public sealed class AngleMathTests
    {
        private const float Tolerance = 1e-4f;

        [TestCase(0f, 1f, 0f)]
        [TestCase(90f, 0f, 1f)]
        [TestCase(180f, -1f, 0f)]
        [TestCase(-90f, 0f, -1f)]
        public void FromAngleDegrees_IsCounterClockwiseFromPlusX(float degrees, float x, float y)
        {
            Vec2 direction = Vec2.FromAngleDegrees(degrees);

            Assert.That(direction.X, Is.EqualTo(x).Within(Tolerance));
            Assert.That(direction.Y, Is.EqualTo(y).Within(Tolerance));
        }

        [Test]
        public void AngleDegrees_InvertsFromAngleDegrees()
        {
            Assert.That(Vec2.FromAngleDegrees(135f).AngleDegrees, Is.EqualTo(135f).Within(Tolerance));
            Assert.That(new Vec2(0f, -3f).AngleDegrees, Is.EqualTo(-90f).Within(Tolerance));
        }

        [Test]
        public void Rotated_TurnsCounterClockwise()
        {
            Vec2 rotated = new Vec2(10f, 0f).Rotated(90f);

            Assert.That(rotated.X, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(rotated.Y, Is.EqualTo(10f).Within(Tolerance));
        }

        [TestCase(10f, 30f, 20f)]
        [TestCase(350f, 10f, 20f)]
        [TestCase(10f, 350f, -20f)]
        [TestCase(0f, 540f, -180f)]
        [TestCase(-170f, 170f, -20f)]
        public void DeltaAngleDegrees_IsTheShortestTurn(float from, float to, float expected)
        {
            Assert.That(XMath.DeltaAngleDegrees(from, to), Is.EqualTo(expected).Within(Tolerance));
        }

        [Test]
        public void LerpAngleDegrees_CrossesTheWrapTheShortWay()
        {
            float middle = XMath.LerpAngleDegrees(170f, -170f, 0.5f);

            Assert.That(XMath.DeltaAngleDegrees(180f, middle), Is.EqualTo(0f).Within(Tolerance));
        }
    }
}
