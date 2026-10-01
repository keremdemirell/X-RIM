using NUnit.Framework;
using XRim.Core;
using XRim.Simulation.Physics;

namespace XRim.Tests.EditMode.Simulation
{
    /// <summary>The weapon arm's hand holds the blade where the physical arm can reach (Session 02).</summary>
    public sealed class ArmReachTests
    {
        private const float Tolerance = 1e-3f;
        private const float Arm = 240f;
        private const float Blade = 400f;
        private static readonly Vec2 Shoulder = Vec2.Zero;

        [Test]
        public void DistanceAtBend_StraightIsTheSumAndFoldedIsTheDifference()
        {
            Assert.That(ArmReach.DistanceAtBend(120f, 120f, 0f), Is.EqualTo(240f).Within(Tolerance));
            Assert.That(ArmReach.DistanceAtBend(150f, 90f, 180f), Is.EqualTo(60f).Within(Tolerance));
            Assert.That(ArmReach.DistanceAtBend(120f, 120f, 90f), Is.EqualTo(169.706f).Within(Tolerance));
        }

        [Test]
        public void GripWithinReach_IsHeldAtTheGrip()
        {
            Assert.That(ArmReach.HandAlongBlade(Shoulder, new Vec2(100f, 0f), Vec2.UnitX, Blade, 80f, Arm), Is.EqualTo(0f));
        }

        [Test]
        public void OnePieceArm_HoldsTheBladeAtArmLength()
        {
            // The grip is 60 out; a rigid 240 arm holds the blade 180 further along, so arm and blade line up.
            float along = ArmReach.HandAlongBlade(Shoulder, new Vec2(60f, 0f), Vec2.UnitX, Blade, Arm, Arm);

            Assert.That(along, Is.EqualTo(180f).Within(Tolerance));
        }

        [Test]
        public void GripBeyondReach_HandSlidesBackTowardTheShoulder()
        {
            float along = ArmReach.HandAlongBlade(Shoulder, new Vec2(300f, 0f), Vec2.UnitX, Blade, 80f, Arm);

            Assert.That(along, Is.EqualTo(-60f).Within(Tolerance));
        }

        [Test]
        public void GripBehindTheShoulder_OnePieceArmHoldsTheBladeInFront()
        {
            // Both points on the line are 240 from the shoulder: 140 behind the grip (off the blade) or 340 ahead (on it).
            float along = ArmReach.HandAlongBlade(Shoulder, new Vec2(-100f, 0f), Vec2.UnitX, Blade, Arm, Arm);

            Assert.That(along, Is.EqualTo(340f).Within(Tolerance));
        }

        [Test]
        public void BladeLineOutOfReach_HoldsTheNearestPoint()
        {
            float along = ArmReach.HandAlongBlade(Shoulder, new Vec2(-50f, 300f), Vec2.UnitX, Blade, Arm, Arm);

            Assert.That(along, Is.EqualTo(50f).Within(Tolerance));
        }

        [Test]
        public void Elbow_BendsForwardAndKeepsBothSegmentLengths()
        {
            var hand = new Vec2(100f, -100f);

            Vec2 elbow = ArmReach.Elbow(Shoulder, hand, 120f, 120f);

            Assert.That(Vec2.Distance(Shoulder, elbow), Is.EqualTo(120f).Within(Tolerance));
            Assert.That(Vec2.Distance(elbow, hand), Is.EqualTo(120f).Within(Tolerance));
            float upperAngle = elbow.AngleDegrees;
            float lowerAngle = (hand - elbow).AngleDegrees;
            Assert.That(XMath.DeltaAngleDegrees(upperAngle, lowerAngle), Is.GreaterThan(0f), "the forearm turns counter-clockwise");
        }

        [Test]
        public void Elbow_ForAStraightArmIsHalfway()
        {
            Vec2 elbow = ArmReach.Elbow(Shoulder, new Vec2(240f, 0f), 120f, 120f);

            Assert.That(elbow.X, Is.EqualTo(120f).Within(Tolerance));
            Assert.That(elbow.Y, Is.EqualTo(0f).Within(Tolerance));
        }
    }
}
