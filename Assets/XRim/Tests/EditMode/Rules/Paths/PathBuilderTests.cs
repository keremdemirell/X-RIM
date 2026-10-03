using System;
using NUnit.Framework;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Paths;
using XRim.Rules.Settings;

namespace XRim.Tests.EditMode.Rules.Paths
{
    /// <summary>
    /// The order of the §6 path rules: stroke, lead-in, resample, reach, ink cut-off, break cut.
    /// With the default settings the shoulder is at (0, 100), so paths along y = 100 run straight out from it.
    /// </summary>
    public sealed class PathBuilderTests
    {
        private const float Tolerance = 1e-2f;

        private readonly PathBuilder _builder = new PathBuilder(new RulePolicies());
        private readonly PathSettings _settings = new PathSettings();

        private static WeaponPath Stroke(params Vec2[] points) => new WeaponPath(points);

        private static Vec2 End(WeaponPath path) => path.Points[path.Points.Count - 1];

        [Test]
        public void D3_LeadInCostsInkAndTime()
        {
            WeaponStats rapier = GddStartingValues.Rapier();
            var tip = new Vec2(100f, 100f);

            BuiltPath built = _builder.Build(WeaponPath.Empty, Stroke(new Vec2(200f, 100f), new Vec2(300f, 100f)), tip,
                rapier, _settings);

            Assert.That(built.Path.Points[0], Is.EqualTo(tip));
            Assert.That(built.Path.LengthUnits, Is.EqualTo(200f).Within(Tolerance), "100 lead-in + 100 drawn");
            Assert.That(built.Inked.Ink.CostUnits, Is.EqualTo(200f).Within(Tolerance));
            float travelSeconds = built.Path.LengthUnits / rapier.SpeedUnitsPerSecond; // §9: t = d / v
            Assert.That(travelSeconds, Is.GreaterThan(100f / rapier.SpeedUnitsPerSecond));
        }

        [Test]
        public void D3_LeadInCountsTowardTheInkBudget()
        {
            WeaponStats rapier = GddStartingValues.Rapier();

            // 100 lead-in, 400 out, then back: the 600 ink runs out 100 into the way back.
            BuiltPath built = _builder.Build(WeaponPath.Empty,
                Stroke(new Vec2(100f, 100f), new Vec2(500f, 100f), new Vec2(100f, 100f)), new Vec2(0f, 100f), rapier, _settings);

            Assert.That(built.Inked.WasCut, Is.True);
            Assert.That(Vec2.Distance(End(built.Path), new Vec2(400f, 100f)), Is.LessThan(Tolerance));
        }

        [Test]
        public void D4_PartsBeyondReachAreClamped_AndCostNoInk()
        {
            WeaponStats rapier = GddStartingValues.Rapier();
            float reach = _settings.ArmLengthUnits + rapier.LengthUnits;

            BuiltPath built = _builder.Build(WeaponPath.Empty, Stroke(new Vec2(1000f, 100f)), new Vec2(100f, 100f), rapier, _settings);

            Assert.That(built.WasClampedByReach, Is.True);
            Assert.That(built.Inked.WasCut, Is.False, "900 drawn, but only the reachable part costs ink");
            Assert.That(Vec2.Distance(End(built.Path), new Vec2(reach, 100f)), Is.LessThan(Tolerance));
            Assert.That(built.Inked.Ink.CostUnits, Is.EqualTo(reach - 100f).Within(Tolerance));
        }

        [Test]
        public void D4_TheShieldReachesOnlyArmsLength_BecauseItIsHeldAtItsCentre()
        {
            WeaponStats shield = GddStartingValues.Shield();

            BuiltPath built = _builder.Build(WeaponPath.Empty, Stroke(new Vec2(1000f, 100f)), new Vec2(100f, 100f), shield, _settings);

            Assert.That(shield.ReachBeyondHandUnits, Is.EqualTo(0f), "A3: the shield adds no reach");
            Assert.That(GddStartingValues.Rapier().ReachBeyondHandUnits, Is.EqualTo(GddStartingValues.Rapier().LengthUnits));
            Assert.That(built.WasClampedByReach, Is.True);
            Assert.That(Vec2.Distance(End(built.Path), new Vec2(_settings.ArmLengthUnits, 100f)), Is.LessThan(Tolerance));
        }

        [Test]
        public void ATap_IsALeadInFromTheTipToTheTappedSpot()
        {
            WeaponStats shield = GddStartingValues.Shield();
            var centre = new Vec2(100f, 100f);

            BuiltPath built = _builder.Build(WeaponPath.Empty, Stroke(new Vec2(130f, 140f)), centre, shield, _settings);

            Assert.That(built.Path.IsEmpty, Is.False, "D3: the lead-in makes a tap a short path");
            Assert.That(built.Path.Points[0], Is.EqualTo(centre));
            Assert.That(Vec2.Distance(End(built.Path), new Vec2(130f, 140f)), Is.LessThan(Tolerance));
            Assert.That(built.Path.LengthUnits, Is.EqualTo(50f).Within(Tolerance));
        }

        [Test]
        public void D5_RedrawingReplacesThePreviousStroke()
        {
            WeaponStats rapier = GddStartingValues.Rapier();
            var tip = new Vec2(0f, 100f);
            BuiltPath first = _builder.Build(WeaponPath.Empty, Stroke(tip, new Vec2(0f, 300f)), tip, rapier, _settings);
            WeaponPath second = Stroke(tip, new Vec2(250f, 100f));

            BuiltPath redrawn = _builder.Build(first.Drawn, second, tip, rapier, _settings);

            Assert.That(redrawn.Drawn, Is.SameAs(second));
            Assert.That(Vec2.Distance(End(redrawn.Path), new Vec2(250f, 100f)), Is.LessThan(Tolerance));
            Assert.That(redrawn.Path.LengthUnits, Is.EqualTo(250f).Within(Tolerance));
        }

        [Test]
        public void SharpTurn_CutsTheSpearsPathAtTheBreak()
        {
            WeaponStats spear = GddStartingValues.Spear();
            var tip = new Vec2(0f, 100f);
            var corner = new Vec2(200f, 100f);
            double turn = 120.0 * Math.PI / 180.0;
            var afterCorner = corner + new Vec2((float)Math.Cos(turn), (float)Math.Sin(turn)) * 100f;

            BuiltPath built = _builder.Build(WeaponPath.Empty, Stroke(tip, corner, afterCorner), tip, spear, _settings);

            Assert.That(built.WasCutAtBreak, Is.True);
            Assert.That(built.Inked.Ink.IsValid, Is.True, "what is left executes");
            Assert.That(Vec2.Distance(End(built.Path), corner), Is.LessThanOrEqualTo(_settings.SampleSpacingUnits));
            Assert.That(Vec2.Distance(End(built.Reachable), afterCorner), Is.LessThan(Tolerance), "the preview still shows the rest");
        }

        [Test]
        public void SharpTurn_DoesNotCutARapier()
        {
            WeaponStats rapier = GddStartingValues.Rapier();
            var tip = new Vec2(0f, 100f);
            var afterCorner = new Vec2(150f, 186.6f);

            BuiltPath built = _builder.Build(WeaponPath.Empty, Stroke(tip, new Vec2(200f, 100f), afterCorner), tip, rapier, _settings);

            Assert.That(built.WasCutAtBreak, Is.False);
            Assert.That(Vec2.Distance(End(built.Path), afterCorner), Is.LessThan(Tolerance));
        }

        [Test]
        public void Thickness_ComesFromTheWeapon_NotTheInput()
        {
            WeaponStats mace = GddStartingValues.Mace();
            var tip = new Vec2(0f, 100f);

            BuiltPath built = _builder.Build(WeaponPath.Empty, Stroke(tip, new Vec2(100f, 100f)), tip, mace, _settings);

            Assert.That(built.Inked.ThicknessUnits, Is.EqualTo(mace.InkThicknessUnits));
        }

        [Test]
        public void Validation_AsksForAWeaponLength()
        {
            RulesSettings settings = GddStartingValues.CreateRulesSettings();
            settings.FindWeapon(WeaponIds.Rapier).LengthUnits = 0f;
            var issues = new System.Collections.Generic.List<string>();

            settings.Validate(issues);

            Assert.That(issues, Has.Some.Contains("positive length"));
        }
    }
}
