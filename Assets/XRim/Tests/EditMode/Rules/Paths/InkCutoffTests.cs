using NUnit.Framework;
using XRim.Core;
using XRim.Rules.Paths;
using XRim.Rules.Settings;

namespace XRim.Tests.EditMode.Rules.Paths
{
    /// <summary>GDD §6 (Decided): drawing stops when the ink runs out; thickness is the weapon's hit width.</summary>
    public sealed class InkCutoffTests
    {
        private const float Tolerance = 1e-3f;

        private readonly InkCutoff _cutoff = new InkCutoff();
        private readonly PathResampler _resampler = new PathResampler();
        private readonly float _spacing = new PathSettings().SampleSpacingUnits;

        private WeaponPath Straight(float lengthUnits) =>
            _resampler.Resample(new[] { Vec2.Zero, new Vec2(lengthUnits, 0f) }, _spacing);

        [Test]
        public void PathLongerThanTheBudget_StopsExactlyWhereTheInkRunsOut()
        {
            WeaponStats rapier = GddStartingValues.Rapier();

            InkedPath inked = _cutoff.Apply(Straight(1000f), rapier, new LengthInkCostModel());

            Assert.That(inked.WasCut, Is.True);
            Assert.That(inked.Path.LengthUnits, Is.EqualTo(rapier.InkLengthUnits).Within(Tolerance));
            Assert.That(inked.Path.Points[inked.Path.Points.Count - 1].X, Is.EqualTo(rapier.InkLengthUnits).Within(Tolerance));
            Assert.That(inked.RemainingUnits, Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void PathWithinTheBudget_IsKept_AndReportsTheInkLeft()
        {
            WeaponStats mace = GddStartingValues.Mace();

            InkedPath inked = _cutoff.Apply(Straight(150f), mace, new LengthInkCostModel());

            Assert.That(inked.WasCut, Is.False);
            Assert.That(inked.Path.LengthUnits, Is.EqualTo(150f).Within(Tolerance));
            Assert.That(inked.RemainingUnits, Is.EqualTo(mace.InkLengthUnits - 150f).Within(Tolerance));
        }

        [Test]
        public void PathUsingExactlyTheWholeBudget_IsNotCut()
        {
            WeaponStats mace = GddStartingValues.Mace();

            InkedPath inked = _cutoff.Apply(Straight(mace.InkLengthUnits), mace, new LengthInkCostModel());

            Assert.That(inked.WasCut, Is.False);
        }

        [Test]
        public void Thickness_ComesFromTheWeapon()
        {
            WeaponStats rapier = GddStartingValues.Rapier();
            WeaponStats mace = GddStartingValues.Mace();

            Assert.That(_cutoff.Apply(Straight(50f), rapier, new LengthInkCostModel()).ThicknessUnits,
                Is.EqualTo(rapier.InkThicknessUnits));
            Assert.That(_cutoff.Apply(Straight(50f), mace, new LengthInkCostModel()).ThicknessUnits,
                Is.EqualTo(mace.InkThicknessUnits));
        }

        [Test]
        public void BendPenalty_UsesUpInkSooner_SoTheSpearsPathEndsShorter()
        {
            WeaponStats spear = GddStartingValues.Spear();
            var zigzag = new Vec2[41];
            for (int i = 0; i < zigzag.Length; i++)
            {
                zigzag[i] = new Vec2(i * 30f, i % 2 == 0 ? 0f : 10f); // every raw corner turns about 37°
            }

            InkedPath inked = _cutoff.Apply(_resampler.Resample(zigzag, _spacing), spear, new RigidityInkCostModel());

            Assert.That(inked.WasCut, Is.True);
            Assert.That(inked.Ink.CostUnits, Is.EqualTo(spear.InkLengthUnits).Within(Tolerance));
            Assert.That(inked.Path.LengthUnits, Is.LessThan(spear.InkLengthUnits));
        }
    }
}
