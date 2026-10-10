using NUnit.Framework;
using XRim.Presentation.Feel;

namespace XRim.Tests.EditMode.Presentation
{
    /// <summary>A landed hit freezes playback for a moment of real time (GDD pillar 1); it never changes what happened.</summary>
    public sealed class HitStopTests
    {
        private const float Tolerance = 1e-6f;

        [Test]
        public void WithoutAHit_PlaybackRunsAtFullPace()
        {
            var hitStop = new HitStop();

            Assert.That(hitStop.IsHolding, Is.False);
            Assert.That(hitStop.Pace(0.016f), Is.EqualTo(0.016f));
        }

        [Test]
        public void AHit_FreezesForItsSeconds_ThenTheRestOfTheFrameRuns()
        {
            var hitStop = new HitStop();
            hitStop.Trigger(0.06f);

            Assert.That(hitStop.IsHolding, Is.True);
            Assert.That(hitStop.Pace(0.05f), Is.EqualTo(0f).Within(Tolerance));
            Assert.That(hitStop.IsHolding, Is.True);
            Assert.That(hitStop.Pace(0.05f), Is.EqualTo(0.04f).Within(Tolerance), "0.01 s left of the freeze, then the timeline moves");
            Assert.That(hitStop.IsHolding, Is.False);
        }

        [Test]
        public void HitsAtTheSameInstant_ShareOneFreeze()
        {
            var hitStop = new HitStop();
            hitStop.Trigger(0.06f);
            hitStop.Trigger(0.06f);

            Assert.That(hitStop.Pace(0.06f), Is.EqualTo(0f).Within(Tolerance));
            Assert.That(hitStop.IsHolding, Is.False);
        }

        [Test]
        public void Reset_DropsAFreezeLeftFromTheLastPlay()
        {
            var hitStop = new HitStop();
            hitStop.Trigger(0.06f);

            hitStop.Reset();

            Assert.That(hitStop.IsHolding, Is.False);
            Assert.That(hitStop.Pace(0.016f), Is.EqualTo(0.016f));
        }
    }
}
