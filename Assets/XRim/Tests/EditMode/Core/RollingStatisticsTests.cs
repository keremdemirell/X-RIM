using System;
using NUnit.Framework;
using XRim.Core;

namespace XRim.Tests.EditMode.Core
{
    /// <summary>The numbers behind the on-device measurements (simulation time per turn, frame time), GDD §18.</summary>
    public sealed class RollingStatisticsTests
    {
        private const double Tolerance = 1e-9;

        private static RollingStatistics Of(int capacity, params double[] values)
        {
            var statistics = new RollingStatistics(capacity);
            foreach (double value in values)
            {
                statistics.Add(value);
            }

            return statistics;
        }

        [Test]
        public void BeforeTheFirstValue_EverythingIsZero()
        {
            var statistics = new RollingStatistics(5);

            Assert.That(statistics.Count, Is.EqualTo(0));
            Assert.That(statistics.Last, Is.EqualTo(0.0));
            Assert.That(statistics.AllTimeMean, Is.EqualTo(0.0));
            Assert.That(statistics.WindowMean, Is.EqualTo(0.0));
            Assert.That(statistics.WindowMax, Is.EqualTo(0.0));
            Assert.That(statistics.Percentile(0.95), Is.EqualTo(0.0));
        }

        [Test]
        public void AllTimeNumbers_CoverEveryValueEverAdded()
        {
            RollingStatistics statistics = Of(2, 10.0, 30.0, 20.0);

            Assert.That(statistics.Count, Is.EqualTo(3));
            Assert.That(statistics.Last, Is.EqualTo(20.0));
            Assert.That(statistics.AllTimeMin, Is.EqualTo(10.0));
            Assert.That(statistics.AllTimeMax, Is.EqualTo(30.0));
            Assert.That(statistics.AllTimeMean, Is.EqualTo(20.0).Within(Tolerance));
        }

        [Test]
        public void TheWindow_KeepsOnlyTheMostRecentValues()
        {
            RollingStatistics statistics = Of(2, 100.0, 10.0, 20.0);

            Assert.That(statistics.WindowCount, Is.EqualTo(2));
            Assert.That(statistics.WindowMean, Is.EqualTo(15.0).Within(Tolerance), "100 has fallen out of the window");
            Assert.That(statistics.WindowMax, Is.EqualTo(20.0));
            Assert.That(statistics.AllTimeMax, Is.EqualTo(100.0), "but all-time numbers still remember it");
        }

        [TestCase(0.5, 3.0)]
        [TestCase(0.95, 5.0)]
        [TestCase(1.0, 5.0)]
        [TestCase(0.0, 1.0)]
        [TestCase(0.2, 1.0)]
        [TestCase(0.21, 2.0)]
        public void Percentile_IsTheNearestRank(double fraction, double expected)
        {
            RollingStatistics statistics = Of(10, 5.0, 1.0, 4.0, 2.0, 3.0);

            Assert.That(statistics.Percentile(fraction), Is.EqualTo(expected));
        }

        [Test]
        public void ANumberThatIsNotFinite_IsIgnored()
        {
            RollingStatistics statistics = Of(5, 10.0, double.NaN, double.PositiveInfinity, 20.0);

            Assert.That(statistics.Count, Is.EqualTo(2));
            Assert.That(statistics.AllTimeMean, Is.EqualTo(15.0).Within(Tolerance));
        }

        [Test]
        public void Clear_StartsOver()
        {
            RollingStatistics statistics = Of(5, 10.0, 20.0);

            statistics.Clear();
            statistics.Add(7.0);

            Assert.That(statistics.Count, Is.EqualTo(1));
            Assert.That(statistics.AllTimeMin, Is.EqualTo(7.0));
            Assert.That(statistics.AllTimeMax, Is.EqualTo(7.0));
            Assert.That(statistics.WindowMean, Is.EqualTo(7.0));
        }

        [Test]
        public void Describe_ReadsTheSameOnEveryDevice_AndSaysWhenThereIsNothing()
        {
            Assert.That(new RollingStatistics(3).Describe("ms"), Is.EqualTo("no measurements yet"));

            string line = Of(4, 12.5, 17.5).Describe("ms");

            Assert.That(line, Does.Contain("2 measured"));
            Assert.That(line, Does.Contain("last 17.5"));
            Assert.That(line, Does.Contain("mean 15.0"), "a decimal point, never a comma, whatever the phone's language");
            Assert.That(line, Does.EndWith("ms"));
        }

        [Test]
        public void ACapacityBelowOne_IsRefused() =>
            Assert.Throws<ArgumentOutOfRangeException>(() => new RollingStatistics(0));
    }
}
