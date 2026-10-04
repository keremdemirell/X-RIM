using System;
using NUnit.Framework;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Paths;
using XRim.Rules.Settings;

namespace XRim.Tests.EditMode.Rules.Paths
{
    /// <summary>
    /// The live preview shows what the rules will make of a stroke (GDD §6): clipped at the reach limit (D4), cut where the ink
    /// runs out, at the weapon's thickness, red where a rigid weapon broke, with the ink left (D7).
    /// </summary>
    public sealed class PathPreviewBuilderTests
    {
        private const float Tolerance = 1e-2f;
        private static readonly Vec2 Tip = new Vec2(0f, 100f);

        private readonly PathPreviewBuilder _preview = new PathPreviewBuilder(new RulePolicies());
        private readonly PathSettings _settings = new PathSettings();

        private static Vec2[] Line(params Vec2[] points) => points;

        private static Vec2 End(WeaponPath path) => path.Points[path.Points.Count - 1];

        [Test]
        public void NothingDrawnYet_AllTheInkIsLeft()
        {
            WeaponStats rapier = GddStartingValues.Rapier();

            PathPreview preview = _preview.Build(new Vec2[0], Tip, rapier, _settings);

            Assert.That(preview.Path.IsEmpty, Is.True);
            Assert.That(preview.InkRemainingFraction, Is.EqualTo(1f));
            Assert.That(preview.InkBudgetUnits, Is.EqualTo(rapier.InkLengthUnits));
            Assert.That(preview.Reach.RadiusUnits, Is.EqualTo(_settings.ArmLengthUnits + rapier.LengthUnits));
        }

        [Test]
        public void D7_TheInkLeftFollowsTheDrawnLength()
        {
            WeaponStats rapier = GddStartingValues.Rapier();

            PathPreview preview = _preview.Build(Line(Tip, new Vec2(150f, 100f)), Tip, rapier, _settings);

            float expectedLeft = (rapier.InkLengthUnits - 150f) / rapier.InkLengthUnits;
            Assert.That(preview.InkSpentUnits, Is.EqualTo(150f).Within(Tolerance));
            Assert.That(preview.InkRemainingFraction, Is.EqualTo(expectedLeft).Within(1e-3f));
            Assert.That(preview.WasCutByInk, Is.False);
        }

        [Test]
        public void D7_WhenTheInkRunsOut_ThePathStopsThere_AndNoInkIsLeft()
        {
            WeaponStats rapier = GddStartingValues.Rapier();

            // 100 lead-in, 400 out, then back: the 600 ink runs out 100 into the way back.
            PathPreview preview = _preview.Build(Line(new Vec2(100f, 100f), new Vec2(500f, 100f), new Vec2(100f, 100f)), Tip, rapier,
                _settings);

            Assert.That(preview.WasCutByInk, Is.True);
            Assert.That(preview.InkRemainingFraction, Is.EqualTo(0f).Within(1e-3f));
            Assert.That(Vec2.Distance(End(preview.Path), new Vec2(400f, 100f)), Is.LessThan(Tolerance));
            Assert.That(preview.Stroke.Points.Count, Is.EqualTo(3), "the stroke itself is shown whole, thin");
        }

        [Test]
        public void D4_AStrokeBeyondReachIsShownClippedAtTheLimit()
        {
            WeaponStats rapier = GddStartingValues.Rapier();
            float reach = _settings.ArmLengthUnits + rapier.LengthUnits;

            // The tip already rests at x = 100, so no lead-in eats the ink before the reach limit does.
            PathPreview preview = _preview.Build(Line(new Vec2(100f, 100f), new Vec2(1000f, 100f)), new Vec2(100f, 100f), rapier, _settings);

            Assert.That(preview.WasClampedByReach, Is.True);
            Assert.That(Vec2.Distance(End(preview.Path), new Vec2(reach, 100f)), Is.LessThan(Tolerance));
            Assert.That(preview.Reach.OriginUnits, Is.EqualTo(_settings.ShoulderOffsetUnits));
        }

        [Test]
        public void Thickness_IsTheWeapons()
        {
            WeaponStats mace = GddStartingValues.Mace();

            PathPreview preview = _preview.Build(Line(new Vec2(100f, 100f)), Tip, mace, _settings);

            Assert.That(preview.ThicknessUnits, Is.EqualTo(mace.InkThicknessUnits));
            Assert.That(preview.InkBudgetUnits, Is.EqualTo(mace.InkLengthUnits));
        }

        [Test]
        public void ATap_ShowsTheLeadInToTheSpot()
        {
            WeaponStats shield = GddStartingValues.Shield();

            PathPreview preview = _preview.Build(Line(new Vec2(120f, 100f)), Tip, shield, _settings);

            Assert.That(preview.Path.IsEmpty, Is.False);
            Assert.That(End(preview.Path), Is.EqualTo(new Vec2(120f, 100f)));
        }

        [Test]
        public void ASharpTurnOfARigidWeapon_ShowsTheBrokenRestAsInvalid()
        {
            WeaponStats spear = GddStartingValues.Spear();
            var corner = new Vec2(200f, 100f);
            double turn = 120.0 * Math.PI / 180.0;
            var afterCorner = corner + new Vec2((float)Math.Cos(turn), (float)Math.Sin(turn)) * 100f;

            PathPreview preview = _preview.Build(Line(Tip, corner, afterCorner), Tip, spear, _settings);

            Assert.That(preview.WasCutAtBreak, Is.True);
            Assert.That(preview.InvalidTail.IsEmpty, Is.False);
            Assert.That(preview.InvalidTail.Points[0], Is.EqualTo(End(preview.Path)));
        }

        [Test]
        public void TheSamePathAsTheAuthorityBuilds()
        {
            WeaponStats rapier = GddStartingValues.Rapier();
            Vec2[] stroke = Line(new Vec2(100f, 120f), new Vec2(300f, 60f), new Vec2(550f, 140f));

            PathPreview preview = _preview.Build(stroke, Tip, rapier, _settings);
            BuiltPath rules = new PathBuilder(new RulePolicies()).Build(WeaponPath.Empty, new WeaponPath(stroke), Tip, rapier, _settings);

            Assert.That(preview.Path.Points, Is.EqualTo(rules.Path.Points));
            Assert.That(preview.InkSpentUnits, Is.EqualTo(rules.Inked.Ink.CostUnits));
        }
    }
}
