using System.Collections.Generic;
using NUnit.Framework;
using XRim.Core;
using XRim.Rules.Paths;
using XRim.Rules.Settings;

namespace XRim.Tests.EditMode.Rules.Paths
{
    /// <summary>GDD §4 (Decided): screen size, resolution and touch rate must not change any budget or outcome.</summary>
    public sealed class PathResamplerTests
    {
        private const float StrokeSeconds = 0.4f;
        private const float PhoneLowResolution = 0.8f;    // pixels per arena unit
        private const float TabletHighResolution = 2.5f;
        private const float SameInkTolerancePercent = 1f;

        private readonly float _spacing = new PathSettings().SampleSpacingUnits;
        private readonly PathResampler _resampler = new PathResampler();
        private readonly LengthInkCostModel _ink = new LengthInkCostModel();

        private float InkOf(float touchRateHz, float pixelsPerArenaUnit)
        {
            List<Vec2> raw = FingerStroke.Sample(FingerStroke.SCurve, StrokeSeconds, touchRateHz, pixelsPerArenaUnit);
            WeaponPath path = _resampler.Resample(raw, _spacing);
            return _ink.Measure(path, GddStartingValues.Rapier()).CostUnits;
        }

        [Test]
        public void SameShape_At60HzAnd120Hz_CostsTheSameInk()
        {
            float at60 = InkOf(60f, TabletHighResolution);
            float at120 = InkOf(120f, TabletHighResolution);
            Assert.That(at120, Is.EqualTo(at60).Within(SameInkTolerancePercent).Percent);
        }

        [Test]
        public void SameShape_AtTwoResolutions_CostsTheSameInk()
        {
            float low = InkOf(120f, PhoneLowResolution);
            float high = InkOf(120f, TabletHighResolution);
            Assert.That(low, Is.EqualTo(high).Within(SameInkTolerancePercent).Percent);
        }

        [Test]
        public void SameShape_60HzLowResolutionAnd120HzHighResolution_CostTheSameInk()
        {
            float worst = InkOf(60f, PhoneLowResolution);
            float best = InkOf(120f, TabletHighResolution);
            Assert.That(worst, Is.EqualTo(best).Within(SameInkTolerancePercent).Percent);
        }

        [Test]
        public void Resampled_PointsAreAtMostOneSpacingApart_AndTheEndsAreKept()
        {
            List<Vec2> raw = FingerStroke.Sample(FingerStroke.SCurve, StrokeSeconds, 60f, TabletHighResolution);
            WeaponPath path = _resampler.Resample(raw, _spacing);

            for (int i = 1; i < path.Points.Count - 1; i++)
            {
                Assert.That(Vec2.Distance(path.Points[i - 1], path.Points[i]), Is.LessThanOrEqualTo(_spacing + 1e-3f));
            }

            Assert.That(path.Points[0], Is.EqualTo(raw[0]));
            Assert.That(path.Points[path.Points.Count - 1], Is.EqualTo(raw[raw.Count - 1]));
        }

        [Test]
        public void StraightLine_GetsAPointEverySpacing_PlusItsEnd()
        {
            WeaponPath path = _resampler.Resample(new[] { Vec2.Zero, new Vec2(95f, 0f) }, 10f);

            Assert.That(path.Points.Count, Is.EqualTo(11));
            Assert.That(path.Points[5].X, Is.EqualTo(50f).Within(1e-4f));
            Assert.That(path.Points[10].X, Is.EqualTo(95f).Within(1e-4f));
            Assert.That(path.LengthUnits, Is.EqualTo(95f).Within(1e-3f));
        }

        [Test]
        public void SamplesFollowArcLength_AcrossRawCorners()
        {
            WeaponPath path = _resampler.Resample(new[] { Vec2.Zero, new Vec2(15f, 0f), new Vec2(15f, 15f) }, 10f);

            Assert.That(path.Points[1], Is.EqualTo(new Vec2(10f, 0f)));
            Assert.That(path.Points[2].X, Is.EqualTo(15f).Within(1e-4f));
            Assert.That(path.Points[2].Y, Is.EqualTo(5f).Within(1e-4f));
            Assert.That(path.Points[3].Y, Is.EqualTo(15f).Within(1e-4f));
        }

        [Test]
        public void FingerPausing_DoesNotChangeThePath()
        {
            var steady = new[] { Vec2.Zero, new Vec2(20f, 0f), new Vec2(40f, 10f) };
            var paused = new[] { Vec2.Zero, new Vec2(20f, 0f), new Vec2(20f, 0f), new Vec2(20f, 0f), new Vec2(40f, 10f) };

            IReadOnlyList<Vec2> a = _resampler.Resample(steady, _spacing).Points;
            IReadOnlyList<Vec2> b = _resampler.Resample(paused, _spacing).Points;

            Assert.That(b, Is.EqualTo(a));
        }

        [Test]
        public void Tap_GivesOnePoint_AndNoInputGivesAnEmptyPath()
        {
            WeaponPath tap = _resampler.Resample(new[] { new Vec2(3f, 4f), new Vec2(3f, 4f) }, _spacing);
            Assert.That(tap.Points.Count, Is.EqualTo(1));
            Assert.That(tap.IsEmpty, Is.True);

            Assert.That(_resampler.Resample(new Vec2[0], _spacing).Points, Is.Empty);
        }
    }
}
