using NUnit.Framework;
using XRim.Core;
using XRim.Input;
using XRim.Rules;
using XRim.Simulation.Physics;

namespace XRim.Tests.EditMode.Input
{
    /// <summary>
    /// GDD §4 (Decided: the body zone covers the player's own dummy; designer 2026-10-11, F3): how far from the player's own screen edge the
    /// zone must reach to cover their dummy, from the frozen pose and what the camera shows. The same for either side and either view,
    /// because the own dummy is always drawn at the own edge; the arms never widen it.
    /// </summary>
    public sealed class BodyZoneCoverTests
    {
        private const float Tolerance = 1e-4f;
        private const float ViewWidthUnits = 1600f;
        private const float MarginUnits = 60f;

        [Test]
        public void TheLeftDummy_IsCoveredFromTheLeftEdgeToJustPastItsFront()
        {
            FighterPose pose = Standing(-350f, Side.Left);

            float cover = BodyZoneCover.FromOwnEdgeFraction(pose, Side.Left, 0f, ViewWidthUnits, MarginUnits);

            // The view runs from -800; the front (the forward knee at -330) plus the margin is at -270: 530 of 1600.
            Assert.That(cover, Is.EqualTo(530f / ViewWidthUnits).Within(Tolerance));
        }

        [Test]
        public void TheRightDummy_IsMeasuredFromTheRightEdge_TheSameWay()
        {
            float left = BodyZoneCover.FromOwnEdgeFraction(Standing(-350f, Side.Left), Side.Left, 0f, ViewWidthUnits, MarginUnits);
            float right = BodyZoneCover.FromOwnEdgeFraction(Standing(350f, Side.Right), Side.Right, 0f, ViewWidthUnits, MarginUnits);

            Assert.That(right, Is.EqualTo(left).Within(Tolerance), "mirror images cover the same share");
        }

        [Test]
        public void AViewCentredElsewhere_ShiftsTheCover()
        {
            float centred = BodyZoneCover.FromOwnEdgeFraction(Standing(-350f, Side.Left), Side.Left, 0f, ViewWidthUnits, MarginUnits);
            float shifted = BodyZoneCover.FromOwnEdgeFraction(Standing(-350f, Side.Left), Side.Left, -200f, ViewWidthUnits, MarginUnits);

            Assert.That(shifted, Is.EqualTo(centred + 200f / ViewWidthUnits).Within(Tolerance));
        }

        [Test]
        public void AWeaponArmReachingForward_DoesNotWidenTheZone()
        {
            FighterPose pose = Standing(-350f, Side.Left);
            float before = BodyZoneCover.FromOwnEdgeFraction(pose, Side.Left, 0f, ViewWidthUnits, MarginUnits);
            pose.Set(BodyPart.RightArm, new BodyPose(new Vec2(-100f, 280f), 0f));

            Assert.That(BodyZoneCover.FromOwnEdgeFraction(pose, Side.Left, 0f, ViewWidthUnits, MarginUnits), Is.EqualTo(before));
        }

        [Test]
        public void ACutLeg_NoLongerCounts()
        {
            FighterPose pose = Standing(-350f, Side.Left);
            pose.Set(BodyPart.LeftLeg, new BodyPose(new Vec2(-150f, 0f), 90f)); // lying on the floor ahead
            pose.MarkSevered(BodyPart.LeftLeg);

            float cover = BodyZoneCover.FromOwnEdgeFraction(pose, Side.Left, 0f, ViewWidthUnits, MarginUnits);

            // Without the forward knee the front is the pelvis at -350: plus the margin, -290, so 510 of 1600.
            Assert.That(cover, Is.EqualTo(510f / ViewWidthUnits).Within(Tolerance));
        }

        [Test]
        public void TheCover_StaysOnTheScreen()
        {
            Assert.That(BodyZoneCover.FromOwnEdgeFraction(Standing(900f, Side.Left), Side.Left, 0f, ViewWidthUnits, MarginUnits), Is.EqualTo(1f));
            Assert.That(BodyZoneCover.FromOwnEdgeFraction(Standing(-1500f, Side.Left), Side.Left, 0f, ViewWidthUnits, MarginUnits), Is.EqualTo(0f));
        }

        /// <summary>A ten-body dummy standing with its pelvis at x, its forward knee 20 ahead of the pelvis.</summary>
        private static FighterPose Standing(float pelvisX, Side side)
        {
            float forward = side.FacingSign();
            var pose = new FighterPose { HasLowerSegments = true };
            pose.Set(BodyPart.Torso, new BodyPose(new Vec2(pelvisX, 180f), 0f));
            pose.Set(BodyPart.Head, new BodyPose(new Vec2(pelvisX, 300f), 0f));
            pose.Set(BodyPart.LeftLeg, new BodyPose(new Vec2(pelvisX, 180f), 0f));
            pose.Set(BodyPart.RightLeg, new BodyPose(new Vec2(pelvisX, 180f), 0f));
            pose.SetLower(BodyPart.LeftLeg, new BodyPose(new Vec2(pelvisX + forward * 20f, 90f), 0f));
            pose.SetLower(BodyPart.RightLeg, new BodyPose(new Vec2(pelvisX - forward * 15f, 90f), 0f));
            pose.Set(BodyPart.LeftArm, new BodyPose(new Vec2(pelvisX, 280f), 0f));
            pose.Set(BodyPart.RightArm, new BodyPose(new Vec2(pelvisX, 280f), 0f));
            return pose;
        }
    }
}
