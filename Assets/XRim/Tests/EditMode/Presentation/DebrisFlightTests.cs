using NUnit.Framework;
using XRim.Core;
using XRim.Presentation.Vfx;

namespace XRim.Tests.EditMode.Presentation
{
    /// <summary>The debris spray's flight (GDD §2, §12; placeholder VFX): an arc that bounces on the floor and comes to rest, as a pure function of time.</summary>
    public sealed class DebrisFlightTests
    {
        private const float Gravity = 3924f;
        private const float Tolerance = 1e-2f;

        private static DebrisFlight Flight(Vec2 start, Vec2 velocity, float bounce = 0.35f, float spin = 0f) =>
            new DebrisFlight(start, velocity, spin, Gravity, 0f, bounce);

        [Test]
        public void ItFliesABallisticArc_BeforeItLands()
        {
            DebrisFlight flight = Flight(new Vec2(0f, 300f), new Vec2(100f, 200f));

            Vec2 at = flight.PositionAt(0.1f, out _);

            Assert.That(at.X, Is.EqualTo(10f).Within(Tolerance));
            Assert.That(at.Y, Is.EqualTo(300f + 20f - 0.5f * Gravity * 0.01f).Within(Tolerance));
        }

        [Test]
        public void ItNeverSinksBelowTheFloor_AndEndsLyingOnIt()
        {
            DebrisFlight flight = Flight(new Vec2(0f, 250f), new Vec2(80f, 300f));

            for (float t = 0f; t < 3f; t += 0.01f)
            {
                Assert.That(flight.PositionAt(t, out _).Y, Is.GreaterThanOrEqualTo(-Tolerance), $"at {t:0.00} s");
            }

            Vec2 resting = flight.PositionAt(5f, out _);
            Assert.That(resting.Y, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(flight.PositionAt(6f, out _), Is.EqualTo(resting), "it lies still");
        }

        [Test]
        public void ItBounces_LosingSpeed()
        {
            DebrisFlight flight = Flight(new Vec2(0f, 200f), Vec2.Zero, bounce: 0.5f);
            float landing = (float)System.Math.Sqrt(2f * 200f / Gravity);

            float peak = 0f;
            for (float t = landing; t < landing + 0.3f; t += 0.002f) peak = System.Math.Max(peak, flight.PositionAt(t, out _).Y);

            Assert.That(peak, Is.GreaterThan(10f), "it leaves the floor again");
            Assert.That(peak, Is.LessThan(200f * 0.5f * 0.5f + 1f), "at half the speed it rises a quarter as high");
        }

        [Test]
        public void ItStartsWhereItFliesOff_EvenAskedForAnEarlierTime()
        {
            DebrisFlight flight = Flight(new Vec2(40f, 120f), new Vec2(200f, 200f), spin: 360f);

            Vec2 start = flight.PositionAt(-1f, out float rotation);

            Assert.That(start, Is.EqualTo(new Vec2(40f, 120f)));
            Assert.That(rotation, Is.EqualTo(0f));
            flight.PositionAt(0.05f, out float turned);
            Assert.That(turned, Is.EqualTo(18f).Within(Tolerance), "it spins while it flies");
        }

        [Test]
        public void TheSameFlight_IsTheSameEveryTime()
        {
            DebrisFlight flight = Flight(new Vec2(0f, 300f), new Vec2(-150f, 250f), spin: -500f);

            Assert.That(flight.PositionAt(0.4f, out float first), Is.EqualTo(flight.PositionAt(0.4f, out float again)));
            Assert.That(first, Is.EqualTo(again), "a replay shows the same spray");
        }
    }
}
