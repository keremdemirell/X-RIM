using System.Collections.Generic;
using NUnit.Framework;
using XRim.Core;
using XRim.Presentation.Vfx;
using XRim.Rules;
using XRim.Rules.Settings;
using XRim.Simulation.Physics;
using XRim.Simulation.Recording;

namespace XRim.Tests.EditMode.Presentation
{
    /// <summary>
    /// How the effects move and are kept (Session 13; presentation only): sparks shoot out, slow and fade; arcs always join their ends;
    /// the pool never holds more pieces than its cap; trails follow a swinging tip and vanish when it rests; the drawn flinch rocks away
    /// from the blow and springs back.
    /// </summary>
    public sealed class VfxMotionTests
    {
        private const float Tolerance = 1e-3f;
        private const float Gravity = 3924f;

        // --- Sparks -------------------------------------------------------------------------------

        [Test]
        public void ASpark_ShootsOut_SlowsDown_AndFadesAwayAtTheEndOfItsLife()
        {
            var spark = new SparkFlight(new Vec2(100f, 100f), new Vec2(600f, 0f), Gravity, 0.3f);

            Assert.That(spark.PositionAt(0f), Is.EqualTo(new Vec2(100f, 100f)));
            Assert.That(spark.PositionAt(0.05f).X, Is.GreaterThan(100f));
            Assert.That(spark.VelocityAt(0.2f).X, Is.LessThan(spark.VelocityAt(0.05f).X), "the air slows it");
            Assert.That(spark.PositionAt(0.2f).Y, Is.LessThan(100f), "it droops");
            Assert.That(spark.AlphaAt(0f), Is.EqualTo(1f));
            Assert.That(spark.AlphaAt(0.15f), Is.EqualTo(0.5f).Within(Tolerance));
            Assert.That(spark.AlphaAt(0.4f), Is.EqualTo(0f));
            Assert.That(spark.IsOverAt(0.3f), Is.True);
        }

        [Test]
        public void ADrop_KnowsWhenItFirstLands()
        {
            var drop = new DebrisFlight(new Vec2(0f, 100f), Vec2.Zero, 0f, Gravity, 0f, 0.3f);

            // Falling 100 from rest: sqrt(2 × 100 / g).
            Assert.That(drop.FirstLandingSeconds, Is.EqualTo((float)System.Math.Sqrt(200.0 / Gravity)).Within(Tolerance));
            Assert.That(drop.PositionAt(drop.FirstLandingSeconds + 0.5f, out _).Y, Is.EqualTo(0f).Within(Tolerance), "then it lies on the floor");
        }

        // --- Arcs ----------------------------------------------------------------------------------

        [Test]
        public void AnArc_JoinsItsEnds_StaysNearTheLine_AndTheSameSeedDrawsTheSameArc()
        {
            var from = new Vec2(0f, 0f);
            var to = new Vec2(0f, 300f);
            var first = new Vec2[12];
            var second = new Vec2[12];

            LightningArc.Fill(from, to, 15f, 42u, first);
            LightningArc.Fill(from, to, 15f, 42u, second);

            Assert.That(first[0], Is.EqualTo(from));
            Assert.That(first[11], Is.EqualTo(to));
            Assert.That(second, Is.EqualTo(first));
            foreach (Vec2 point in first)
            {
                Assert.That(System.Math.Abs(point.X), Is.LessThanOrEqualTo(15f + Tolerance), "sideways within the jitter");
            }
        }

        // --- The pool ------------------------------------------------------------------------------

        [Test]
        public void ThePool_NeverHoldsMoreThanItsCap_AndReusesTheOldestPieceFirst()
        {
            var slots = new VfxSlots(3);
            int first = slots.Acquire();
            int second = slots.Acquire();
            int third = slots.Acquire();

            Assert.That(slots.ActiveCount, Is.EqualTo(3));
            Assert.That(slots.Acquire(), Is.EqualTo(first), "full: the oldest is reused");
            Assert.That(slots.ActiveCount, Is.EqualTo(3));

            slots.Release(third);
            Assert.That(slots.Acquire(), Is.EqualTo(third), "a free slot before reusing one");
            Assert.That(slots.Acquire(), Is.EqualTo(second), "then the oldest again");

            slots.Clear();
            Assert.That(slots.ActiveCount, Is.EqualTo(0));
        }

        // --- Trails --------------------------------------------------------------------------------

        [Test]
        public void ATrail_FollowsTheSwingingTip_OverItsWindow_OldestFirst()
        {
            TurnTimeline timeline = Swing(900f);
            var points = new List<Vec2>();

            WeaponTrail.Collect(timeline, Side.Left, SimTime.FromSeconds(0.1), 0.05f, 200f, Rapier(), points);

            Assert.That(points.Count, Is.EqualTo(13), "0.05 s at 240 Hz, both ends");
            Assert.That(points[0].X, Is.LessThan(points[points.Count - 1].X), "oldest first");
            Assert.That(points[points.Count - 1].X, Is.EqualTo(400f + 900f * 0.1f).Within(1f), "ends at the tip now");
        }

        [Test]
        public void ARestingOrSlowWeapon_LeavesNoTrail()
        {
            var points = new List<Vec2>();

            WeaponTrail.Collect(Swing(0f), Side.Left, SimTime.FromSeconds(0.1), 0.05f, 200f, Rapier(), points);
            Assert.That(points, Is.Empty);

            WeaponTrail.Collect(Swing(100f), Side.Left, SimTime.FromSeconds(0.1), 0.05f, 200f, Rapier(), points);
            Assert.That(points, Is.Empty);
        }

        // --- The drawn flinch ----------------------------------------------------------------------

        [Test]
        public void TheFlinch_KicksOutFast_ThenSpringsBackToNothing()
        {
            Assert.That(HitFlinch.StrengthAt(0f, 0.3f), Is.EqualTo(0f));
            Assert.That(HitFlinch.StrengthAt(0.045f, 0.3f), Is.EqualTo(1f).Within(Tolerance), "the peak");
            Assert.That(HitFlinch.StrengthAt(0.2f, 0.3f), Is.GreaterThan(0f).And.LessThan(1f));
            Assert.That(HitFlinch.StrengthAt(0.3f, 0.3f), Is.EqualTo(0f));
            Assert.That(HitFlinch.StrengthAt(-0.1f, 0.3f), Is.EqualTo(0f), "not before the hit");
        }

        [Test]
        public void TheFlinch_RocksAwayFromTheAttacker_ByTheDamage_UpToTheMost()
        {
            Assert.That(HitFlinch.AngleDegrees(Side.Right, 10f, 0.4f, 9f), Is.EqualTo(-4f).Within(Tolerance), "the right dummy tips to the right");
            Assert.That(HitFlinch.AngleDegrees(Side.Left, 10f, 0.4f, 9f), Is.EqualTo(4f).Within(Tolerance));
            Assert.That(HitFlinch.AngleDegrees(Side.Left, 100f, 0.4f, 9f), Is.EqualTo(9f));
            Assert.That(HitFlinch.PushUnits(Side.Right, 10f, 0.6f, 12f), Is.EqualTo(6f).Within(Tolerance), "pushed to the right");
            Assert.That(HitFlinch.PushUnits(Side.Left, 10f, 0.6f, 12f), Is.EqualTo(-6f).Within(Tolerance));
        }

        [Test]
        public void TheFlinch_TurnsAPartAboutThePelvis()
        {
            var head = new BodyPose(new Vec2(600f, 120f), 0f);

            BodyPose flinched = HitFlinch.Apply(head, new Vec2(600f, 0f), -90f, new Vec2(5f, 0f));

            Assert.That(flinched.PositionUnits.X, Is.EqualTo(725f).Within(Tolerance), "a quarter turn clockwise about the pelvis, then the push");
            Assert.That(flinched.PositionUnits.Y, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(flinched.RotationDegrees, Is.EqualTo(-90f));
            Assert.That(HitFlinch.Apply(head, Vec2.Zero, 0f, Vec2.Zero), Is.EqualTo(head), "no flinch, no change");
        }

        private static HeldItemShape Rapier() => HeldItemShape.Of(GddStartingValues.CreateRulesSettings().FindWeapon(WeaponIds.Rapier));

        /// <summary>The left dummy's rapier grip moving along +X at a speed, recorded at 240 Hz for 0.2 s.</summary>
        private static TurnTimeline Swing(float speed)
        {
            var recorder = new TimelineRecorder();
            const int steps = 48;
            for (int step = 0; step <= steps; step++)
            {
                double seconds = step / 240.0;
                var pose = new PoseSnapshot();
                pose.Left.HeldItem = new BodyPose(new Vec2((float)(speed * seconds), 0f), 0f);
                recorder.RecordFrame(step, SimTime.FromSeconds(seconds), pose);
            }

            return recorder.Build();
        }
    }
}
