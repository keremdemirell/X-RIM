using NUnit.Framework;
using XRim.Core;
using XRim.Rules.Paths;
using XRim.Simulation.Drivers;

namespace XRim.Tests.EditMode.Simulation.Drivers
{
    public sealed class PathCursorTests
    {
        private const float Tolerance = 1e-3f;

        private readonly PathCursor _cursor = new PathCursor(new WeaponPath(new[]
        {
            new Vec2(0f, 0f), new Vec2(100f, 0f), new Vec2(100f, 50f),
        }));

        [TestCase(0f, 0f, 0f)]
        [TestCase(50f, 50f, 0f)]
        [TestCase(100f, 100f, 0f)]
        [TestCase(125f, 100f, 25f)]
        [TestCase(150f, 100f, 50f)]
        public void PointAt_FollowsArcLength(float distance, float x, float y)
        {
            Vec2 point = _cursor.PointAt(distance);

            Assert.That(point.X, Is.EqualTo(x).Within(Tolerance));
            Assert.That(point.Y, Is.EqualTo(y).Within(Tolerance));
        }

        [Test]
        public void PointAt_ClampsToTheEnds()
        {
            Assert.That(_cursor.PointAt(-5f), Is.EqualTo(new Vec2(0f, 0f)));
            Assert.That(_cursor.PointAt(500f), Is.EqualTo(new Vec2(100f, 50f)));
            Assert.That(_cursor.LengthUnits, Is.EqualTo(150f).Within(Tolerance));
        }

        [Test]
        public void EmptyPath_HasNoLength()
        {
            var cursor = new PathCursor(WeaponPath.Empty);

            Assert.That(cursor.IsEmpty, Is.True);
            Assert.That(cursor.LengthUnits, Is.EqualTo(0f));
        }
    }
}
