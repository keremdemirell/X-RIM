using NUnit.Framework;
using XRim.Core;

namespace XRim.Tests.EditMode.Core
{
    public sealed class CoreSanityTests
    {
        [Test]
        public void SimTime_FromMilliseconds_StoresWholeMicroseconds()
        {
            Assert.That(SimTime.FromMilliseconds(1.5).Microseconds, Is.EqualTo(1500L));
            Assert.That(SimTime.FromSeconds(1.5) > SimTime.FromMilliseconds(1499.999), Is.True);
        }

        [Test]
        public void Vec2_Length_IsEuclidean()
        {
            Assert.That(new Vec2(3f, 4f).Length, Is.EqualTo(5f).Within(1e-5f));
        }

        [Test]
        public void XorShiftRandom_SameSeed_GivesSameSequence()
        {
            var a = new XorShiftRandom(42u);
            var b = new XorShiftRandom(42u);
            for (int i = 0; i < 100; i++)
            {
                Assert.That(a.NextInt(0, 1000), Is.EqualTo(b.NextInt(0, 1000)));
                float value = a.NextFloat();
                Assert.That(value, Is.EqualTo(b.NextFloat()));
                Assert.That(value, Is.GreaterThanOrEqualTo(0f).And.LessThan(1f));
            }
        }
    }
}
