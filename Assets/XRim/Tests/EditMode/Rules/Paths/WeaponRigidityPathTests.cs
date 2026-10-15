using System;
using System.Collections.Generic;
using NUnit.Framework;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Paths;
using XRim.Rules.Settings;

namespace XRim.Tests.EditMode.Rules.Paths
{
    /// <summary>
    /// GDD §6 spear rigidity through the whole path pipeline (Session 14; designer 2026-10-15: keep it, per weapon): the spear
    /// pays extra ink for a tight S-curve and a zigzag breaks its path, while the rapier is "very tolerant of curves, S-shapes and
    /// zigzags". With the shoulder at (0, 100) a stroke along y = 100 runs straight out from it, and the tip rests at its start.
    /// </summary>
    public sealed class WeaponRigidityPathTests
    {
        private const float Tolerance = 1e-2f;
        private const float StrokePointSpacingUnits = 2f;
        private static readonly Vec2 Tip = new Vec2(0f, 100f);

        private readonly PathBuilder _builder = new PathBuilder(new RulePolicies());
        private readonly PathPreviewBuilder _preview = new PathPreviewBuilder(new RulePolicies());
        private readonly PathSettings _settings = new PathSettings();

        /// <summary>A sine wave leaving the tip along +X: <paramref name="amplitudeUnits"/> either side of the line.</summary>
        private static Vec2[] Wave(float amplitudeUnits, float wavelengthUnits, float lengthUnits)
        {
            var points = new List<Vec2>();
            for (float x = 0f; x <= lengthUnits + 1e-3f; x += StrokePointSpacingUnits)
            {
                float y = Tip.Y + amplitudeUnits * (float)Math.Sin(2.0 * Math.PI * x / wavelengthUnits);
                points.Add(new Vec2(x, y));
            }

            return points.ToArray();
        }

        private BuiltPath Build(Vec2[] stroke, WeaponStats weapon) =>
            _builder.Build(WeaponPath.Empty, new WeaponPath(stroke), Tip, weapon, _settings);

        private static float CostPerUnit(BuiltPath built) => built.Inked.Ink.CostUnits / built.Inked.Ink.LengthUnits;

        // --- The S-curve: the spear pays, the rapier does not -----------------------------------------

        [Test]
        public void ATightSCurve_CostsTheSpearMoreInkThanItsLength_ButStillExecutesWhole()
        {
            BuiltPath built = Build(Wave(40f, 140f, 140f), GddStartingValues.Spear());

            Assert.That(CostPerUnit(built), Is.GreaterThan(1.15f), "the bend penalty is paid on the tight turns");
            Assert.That(built.Inked.Ink.IsValid, Is.True);
            Assert.That(built.WasCutAtBreak, Is.False, "a bend is not a break");
            Assert.That(built.Inked.WasCut, Is.False);
            Assert.That(built.InvalidTail.IsEmpty, Is.True);
        }

        [Test]
        public void TheSameSCurve_CostsTheRapierItsPlainLength_AndNeverBreaks()
        {
            BuiltPath built = Build(Wave(40f, 140f, 140f), GddStartingValues.Rapier());

            Assert.That(built.Inked.Ink.CostUnits, Is.EqualTo(built.Inked.Ink.LengthUnits).Within(Tolerance));
            Assert.That(built.Inked.Ink.IsValid, Is.True);
            Assert.That(built.WasCutAtBreak, Is.False);
        }

        [Test]
        public void AGentleCurve_IsFreeEvenForTheSpear_BecauseEachSampleTurnsBelowTheThreshold()
        {
            BuiltPath built = Build(Wave(20f, 400f, 400f), GddStartingValues.Spear());

            Assert.That(built.Inked.Ink.CostUnits, Is.EqualTo(built.Inked.Ink.LengthUnits).Within(Tolerance));
        }

        [Test]
        public void ATighterCurve_CostsTheSpearMorePerUnitOfPath()
        {
            WeaponStats spear = GddStartingValues.Spear();

            float wide = CostPerUnit(Build(Wave(40f, 240f, 240f), spear));
            float tight = CostPerUnit(Build(Wave(40f, 140f, 140f), spear));

            Assert.That(tight, Is.GreaterThan(wide));
        }

        // --- The zigzag: the spear's path breaks, the rapier's does not --------------------------------

        [Test]
        public void AZigzag_BreaksTheSpearsPath_AndThePreviewDrawsTheRestInvalid()
        {
            WeaponStats spear = GddStartingValues.Spear();
            Vec2[] zigzag = Wave(40f, 80f, 240f);

            BuiltPath built = Build(zigzag, spear);
            PathPreview preview = _preview.Build(zigzag, Tip, spear, _settings);

            Assert.That(built.WasCutAtBreak, Is.True);
            Assert.That(built.Inked.Ink.IsValid, Is.True, "what is left executes");
            Assert.That(built.InvalidTail.IsEmpty, Is.False);
            Assert.That(built.InvalidTail.Points[0], Is.EqualTo(built.Path.Points[built.Path.Points.Count - 1]),
                "the red part starts where the executing path stops");
            Assert.That(preview.WasCutAtBreak, Is.True);
            Assert.That(preview.InvalidTail.IsEmpty, Is.False, "the live preview draws it red");
        }

        [Test]
        public void TheSameZigzag_IsFineForTheRapier_WholeAndAtPlainCost()
        {
            WeaponStats rapier = GddStartingValues.Rapier();
            Vec2[] zigzag = Wave(40f, 80f, 240f);

            BuiltPath built = Build(zigzag, rapier);
            PathPreview preview = _preview.Build(zigzag, Tip, rapier, _settings);

            Assert.That(built.WasCutAtBreak, Is.False);
            Assert.That(built.InvalidTail.IsEmpty, Is.True);
            Assert.That(built.Inked.Ink.CostUnits, Is.EqualTo(built.Inked.Ink.LengthUnits).Within(Tolerance));
            Assert.That(preview.InvalidTail.IsEmpty, Is.True);
        }

        // --- The switch and the per-weapon stat -----------------------------------------------------

        [Test]
        public void CuttingTheSpearsRigidityInTuning_MakesItPayPlainLengthAndNeverBreak()
        {
            WeaponStats spear = GddStartingValues.Spear();
            spear.Rigidity.Enabled = false;

            BuiltPath zigzag = Build(Wave(40f, 80f, 240f), spear);
            BuiltPath curve = Build(Wave(40f, 140f, 140f), spear);

            Assert.That(zigzag.WasCutAtBreak, Is.False);
            Assert.That(curve.Inked.Ink.CostUnits, Is.EqualTo(curve.Inked.Ink.LengthUnits).Within(Tolerance));
        }

        [Test]
        public void RigidityIsAPerWeaponStat_SoAnyWeaponCanCarryIt()
        {
            WeaponStats sword = GddStartingValues.Sword();
            Vec2[] curve = Wave(40f, 140f, 140f);
            Assert.That(CostPerUnit(Build(curve, sword)), Is.EqualTo(1f).Within(1e-3f), "off by default");

            sword.Rigidity.Enabled = true;

            Assert.That(CostPerUnit(Build(curve, sword)), Is.GreaterThan(1.15f));
        }

        [Test]
        public void TheSpearsBendPenalty_IsStrongerThanThePenaltyOfARapierWithATinyOne()
        {
            WeaponStats spear = GddStartingValues.Spear();
            WeaponStats rapier = GddStartingValues.Rapier();
            rapier.Rigidity.Enabled = true;
            rapier.Rigidity.BendCostK = 0.002f;
            rapier.Rigidity.BreakAngleDegrees = 150f;
            Vec2[] curve = Wave(40f, 140f, 140f);

            float spearCost = CostPerUnit(Build(curve, spear));
            float rapierCost = CostPerUnit(Build(curve, rapier));

            Assert.That(rapierCost, Is.GreaterThan(1f), "almost no penalty, not none");
            Assert.That(rapierCost - 1f, Is.LessThan((spearCost - 1f) * 0.2f));
        }
    }
}
