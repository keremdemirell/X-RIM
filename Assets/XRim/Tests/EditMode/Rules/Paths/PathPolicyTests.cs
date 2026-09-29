using System.Collections.Generic;
using NUnit.Framework;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Paths;
using XRim.Rules.Settings;

namespace XRim.Tests.EditMode.Rules.Paths
{
    /// <summary>GDD §6 path rules, decided by the designer on 2026-09-29: D3 path start, D4 reach limit, D5 strokes.</summary>
    public sealed class PathPolicyTests
    {
        private const float Tolerance = 1e-3f;
        private const float Spacing = 10f;

        private readonly PathResampler _resampler = new PathResampler();

        [Test]
        public void RulePolicies_DefaultToTheDecidedPathRules()
        {
            var policies = new RulePolicies();

            Assert.That(policies.PathStart, Is.InstanceOf<LeadInFromTipPathStartPolicy>());
            Assert.That(policies.Reach, Is.InstanceOf<ClampToReachPolicy>());
            Assert.That(policies.Stroke, Is.InstanceOf<ReplaceStrokePolicy>());
            Assert.That(policies.InkCost, Is.InstanceOf<RigidityInkCostModel>());
        }

        [Test]
        public void D5_RedrawingReplacesTheStroke()
        {
            var first = new WeaponPath(new[] { Vec2.Zero, new Vec2(50f, 0f) });
            var second = new WeaponPath(new[] { new Vec2(0f, 20f), new Vec2(40f, 60f) });

            Assert.That(new ReplaceStrokePolicy().Combine(first, second), Is.SameAs(second));
        }

        [Test]
        public void D3_StrokeCanStartAnywhere_WithALeadInFromTheTip()
        {
            var tip = new Vec2(80f, 100f);
            var drawn = new WeaponPath(new[] { new Vec2(150f, 60f), new Vec2(250f, 60f) });

            WeaponPath started = new LeadInFromTipPathStartPolicy().ResolveStart(drawn, tip);

            Assert.That(started.Points, Is.EqualTo(new[] { tip, drawn.Points[0], drawn.Points[1] }));
            Assert.That(started.LengthUnits, Is.EqualTo(Vec2.Distance(tip, drawn.Points[0]) + 100f).Within(Tolerance));
        }

        [Test]
        public void D3_NoStroke_MeansNoLeadIn()
        {
            WeaponPath started = new LeadInFromTipPathStartPolicy().ResolveStart(WeaponPath.Empty, new Vec2(80f, 100f));

            Assert.That(started.Points, Is.Empty);
        }

        [Test]
        public void D4_ReachIsArmLengthPlusWeaponLength_AroundTheShoulder_WithoutTheLunge()
        {
            var paths = new PathSettings();
            WeaponStats rapier = GddStartingValues.Rapier();
            WeaponStats mace = GddStartingValues.Mace();

            ReachLimit rapierReach = ReachLimit.For(paths, rapier);

            Assert.That(rapierReach.OriginUnits, Is.EqualTo(paths.ShoulderOffsetUnits));
            Assert.That(rapierReach.RadiusUnits, Is.EqualTo(paths.ArmLengthUnits + rapier.LengthUnits));
            Assert.That(ReachLimit.For(paths, mace).RadiusUnits, Is.LessThan(rapierReach.RadiusUnits), "the rapier is long, the mace short");
        }

        [Test]
        public void D4_PathWithinReach_IsUnchanged()
        {
            WeaponPath path = _resampler.Resample(new[] { Vec2.Zero, new Vec2(60f, 30f) }, Spacing);

            ReachResult result = new ClampToReachPolicy().Apply(path, new ReachLimit(Vec2.Zero, 100f), Spacing);

            Assert.That(result.WasClamped, Is.False);
            Assert.That(result.Path, Is.SameAs(path));
        }

        [Test]
        public void D4_PathBeyondReach_IsClampedOntoTheLimit_AndContinuesWhenItComesBack()
        {
            var limit = new ReachLimit(Vec2.Zero, 100f);
            var end = new Vec2(80f, 0f);
            WeaponPath path = _resampler.Resample(new[] { Vec2.Zero, new Vec2(0f, 150f), end }, Spacing);

            ReachResult result = new ClampToReachPolicy().Apply(path, limit, Spacing);

            Assert.That(result.WasClamped, Is.True);
            IReadOnlyList<Vec2> points = result.Path.Points;
            foreach (Vec2 point in points)
            {
                Assert.That(Vec2.Distance(point, limit.OriginUnits), Is.LessThanOrEqualTo(limit.RadiusUnits + Tolerance));
            }

            Assert.That(Vec2.Distance(points[points.Count - 1], end), Is.LessThan(Tolerance), "the downswing is kept");
            Assert.That(points, Has.Some.Matches<Vec2>(point => Vec2.Distance(point, new Vec2(0f, 100f)) < Tolerance),
                "the path touches the limit where it left it");
            for (int i = 1; i < points.Count; i++)
            {
                Assert.That(Vec2.Distance(points[i - 1], points[i]), Is.LessThanOrEqualTo(Spacing + Tolerance));
            }
        }
    }
}
