using System;
using System.Collections.Generic;
using NUnit.Framework;
using XRim.Core;
using XRim.Rules.Paths;
using XRim.Rules.Settings;

namespace XRim.Tests.EditMode.Rules.Paths
{
    /// <summary>GDD §6 spear rigidity: cost = distance × (1 + k·Δθ) above the threshold; a very sharp turn breaks the path.</summary>
    public sealed class RigidityInkCostModelTests
    {
        private const float Tolerance = 1e-3f;
        private const float Spacing = 10f;

        private readonly RigidityInkCostModel _model = new RigidityInkCostModel();
        private readonly PathResampler _resampler = new PathResampler();

        /// <summary>Three segments of 10 units; the middle point turns by <paramref name="turnDegrees"/>.</summary>
        private static WeaponPath Bend(float turnDegrees)
        {
            float radians = turnDegrees * XMath.DegreesToRadians;
            var direction = new Vec2((float)Math.Cos(radians), (float)Math.Sin(radians));
            var b = new Vec2(10f, 0f);
            var c = new Vec2(20f, 0f);
            return new WeaponPath(new[] { Vec2.Zero, b, c, c + direction * 10f });
        }

        [Test]
        public void TurnAboveThreshold_CostsDistanceTimesOnePlusKTimesTurn()
        {
            WeaponStats spear = GddStartingValues.Spear();
            const float turn = 30f;

            InkMeasurement ink = _model.Measure(Bend(turn), spear);

            float expected = 10f + 10f + 10f * (1f + spear.Rigidity.BendCostK * turn);
            Assert.That(ink.CostUnits, Is.EqualTo(expected).Within(Tolerance));
            Assert.That(ink.LengthUnits, Is.EqualTo(30f).Within(Tolerance));
            Assert.That(ink.IsValid, Is.True);
        }

        [Test]
        public void TurnAtOrBelowThreshold_CostsPlainLength()
        {
            WeaponStats spear = GddStartingValues.Spear();

            InkMeasurement ink = _model.Measure(Bend(spear.Rigidity.BendThresholdDegrees - 1f), spear);

            Assert.That(ink.CostUnits, Is.EqualTo(ink.LengthUnits).Within(Tolerance));
        }

        [Test]
        public void CostJumpsAtTheThreshold_BecauseTheWholeTurnCounts()
        {
            WeaponStats spear = GddStartingValues.Spear();
            float threshold = spear.Rigidity.BendThresholdDegrees;

            float justAbove = _model.Measure(Bend(threshold + 0.5f), spear).CostUnits;

            float wholeTurnPenalty = 10f * spear.Rigidity.BendCostK * (threshold + 0.5f);
            Assert.That(justAbove, Is.EqualTo(30f + wholeTurnPenalty).Within(Tolerance));
        }

        [Test]
        public void WeaponWithoutRigidity_PaysPlainLength_AndNeverBreaks()
        {
            WeaponStats rapier = GddStartingValues.Rapier();
            WeaponPath hairpin = _resampler.Resample(FingerStroke.Corner(100f, 170f), Spacing);

            InkMeasurement ink = _model.Measure(hairpin, rapier);

            Assert.That(rapier.Rigidity.Enabled, Is.False);
            Assert.That(ink.CostUnits, Is.EqualTo(ink.LengthUnits).Within(Tolerance));
            Assert.That(ink.IsValid, Is.True);
        }

        [Test]
        public void RigidityFlagOff_TurnsTheRuleOffForTheSpear()
        {
            WeaponStats spear = GddStartingValues.Spear();
            spear.Rigidity.Enabled = false;
            WeaponPath hairpin = _resampler.Resample(FingerStroke.Corner(100f, 170f), Spacing);

            InkMeasurement ink = _model.Measure(hairpin, spear);

            Assert.That(ink.CostUnits, Is.EqualTo(ink.LengthUnits).Within(Tolerance));
            Assert.That(ink.IsValid, Is.True);
        }

        [TestCase(0f)]
        [TestCase(2.5f)]
        [TestCase(5f)]
        [TestCase(7.5f)]
        public void SharpCorner_BreaksThePath_WhereverItFallsBetweenSamples(float cornerOffsetUnits)
        {
            WeaponStats spear = GddStartingValues.Spear();
            List<Vec2> raw = FingerStroke.Corner(100f + cornerOffsetUnits, 120f);
            WeaponPath path = _resampler.Resample(raw, Spacing);

            InkMeasurement ink = _model.Measure(path, spear);

            Assert.That(ink.IsValid, Is.False, "a 120° corner is sharper than the 90° break angle");
            Vec2 breakPoint = path.Points[ink.FirstInvalidPointIndex];
            Assert.That(Vec2.Distance(breakPoint, raw[1]), Is.LessThanOrEqualTo(Spacing + Tolerance));
        }

        [TestCase(0f)]
        [TestCase(2.5f)]
        [TestCase(5f)]
        [TestCase(7.5f)]
        public void CornerBelowTheBreakAngle_NeverBreaks_ButPaysTheBend(float cornerOffsetUnits)
        {
            WeaponStats spear = GddStartingValues.Spear();
            WeaponPath path = _resampler.Resample(FingerStroke.Corner(100f + cornerOffsetUnits, 60f), Spacing);

            InkMeasurement ink = _model.Measure(path, spear);

            Assert.That(ink.IsValid, Is.True);
            Assert.That(ink.CostUnits, Is.GreaterThan(ink.LengthUnits));
        }

        [Test]
        public void GentleZigzag_DoesNotAddUpToABreak()
        {
            WeaponStats spear = GddStartingValues.Spear();
            var zigzag = new WeaponPath(new[]
            {
                Vec2.Zero, new Vec2(10f, 0f), new Vec2(18.66f, 5f), new Vec2(28.66f, 5f), new Vec2(37.32f, 0f),
            });

            Assert.That(_model.Measure(zigzag, spear).IsValid, Is.True);
        }

        [Test]
        public void CumulativeCost_StartsAtZero_AndEndsAtTheTotal()
        {
            WeaponStats spear = GddStartingValues.Spear();

            InkMeasurement ink = _model.Measure(Bend(45f), spear);

            Assert.That(ink.CumulativeCostUnits.Count, Is.EqualTo(4));
            Assert.That(ink.CumulativeCostUnits[0], Is.EqualTo(0f));
            Assert.That(ink.CumulativeCostUnits[3], Is.EqualTo(ink.CostUnits));
        }
    }
}
