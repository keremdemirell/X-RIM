using NUnit.Framework;
using XRim.Core;
using XRim.Rules.Paths;
using XRim.Rules.Settings;

namespace XRim.Tests.EditMode.Rules.Paths
{
    /// <summary>
    /// The infinite-ink cheat (Session 13): a path costs no ink, so nothing is cut for its ink, but it is measured as before and a
    /// too-sharp spear turn still breaks it.
    /// </summary>
    public sealed class FreeInkCostModelTests
    {
        private const float Tolerance = 1e-3f;

        private readonly PathResampler _resampler = new PathResampler();
        private readonly float _spacing = new PathSettings().SampleSpacingUnits;

        private WeaponPath Straight(float lengthUnits) =>
            _resampler.Resample(new[] { Vec2.Zero, new Vec2(lengthUnits, 0f) }, _spacing);

        [Test]
        public void APath_CostsNoInk_ButKeepsItsLength()
        {
            WeaponPath path = Straight(1000f);

            InkMeasurement ink = new FreeInkCostModel(new LengthInkCostModel()).Measure(path, GddStartingValues.Rapier());

            Assert.That(ink.CostUnits, Is.EqualTo(0f));
            Assert.That(ink.LengthUnits, Is.EqualTo(1000f).Within(Tolerance));
            Assert.That(ink.CumulativeCostUnits.Count, Is.EqualTo(path.Points.Count), "one entry per point, as the cut-off expects");
        }

        [Test]
        public void APathThreeTimesTheBudget_IsNotCut()
        {
            WeaponStats rapier = GddStartingValues.Rapier();
            WeaponPath path = Straight(rapier.InkLengthUnits * 3f);

            InkedPath inked = new InkCutoff().Apply(path, rapier, new FreeInkCostModel(new LengthInkCostModel()));

            Assert.That(inked.WasCut, Is.False);
            Assert.That(inked.Path.LengthUnits, Is.EqualTo(path.LengthUnits).Within(Tolerance));
        }

        [Test]
        public void ATooSharpSpearTurn_StillBreaksThePath()
        {
            WeaponStats spear = GddStartingValues.Spear();
            WeaponPath hairpin = _resampler.Resample(FingerStroke.Corner(100f, 170f), _spacing);
            var inner = new RigidityInkCostModel();

            InkMeasurement free = new FreeInkCostModel(inner).Measure(hairpin, spear);

            Assert.That(free.IsValid, Is.False);
            Assert.That(free.FirstInvalidPointIndex, Is.EqualTo(inner.Measure(hairpin, spear).FirstInvalidPointIndex));
        }
    }
}
