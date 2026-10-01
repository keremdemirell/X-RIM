using NUnit.Framework;
using XRim.Core;
using XRim.Rules;
using XRim.Simulation.Drivers;
using XRim.Simulation.Physics;

namespace XRim.Tests.EditMode.Simulation.Drivers
{
    /// <summary>GDD §6 (Decided): paths are drawn relative to the torso, +X toward the opponent, and move with the body.</summary>
    public sealed class TorsoFrameTests
    {
        private const float Tolerance = 1e-3f;

        private static void AssertNear(Vec2 actual, Vec2 expected)
        {
            Assert.That(actual.X, Is.EqualTo(expected.X).Within(Tolerance), "x");
            Assert.That(actual.Y, Is.EqualTo(expected.Y).Within(Tolerance), "y");
        }

        [Test]
        public void LeftFighter_FrameIsTheTorsoPosition()
        {
            var torso = new BodyPose(new Vec2(-350f, 180f), 0f);

            AssertNear(TorsoFrame.ToArena(new Vec2(100f, 50f), torso, Side.Left), new Vec2(-250f, 230f));
        }

        [Test]
        public void RightFighter_PlusXPointsTowardTheOpponent()
        {
            var torso = new BodyPose(new Vec2(350f, 180f), 0f);

            AssertNear(TorsoFrame.ToArena(new Vec2(100f, 50f), torso, Side.Right), new Vec2(250f, 230f));
            Assert.That(TorsoFrame.AngleToArena(30f, torso, Side.Right), Is.EqualTo(150f).Within(Tolerance));
        }

        [Test]
        public void Frame_TurnsWithTheTorso()
        {
            var torso = new BodyPose(Vec2.Zero, 90f);

            AssertNear(TorsoFrame.ToArena(new Vec2(10f, 0f), torso, Side.Left), new Vec2(0f, 10f));
            Assert.That(TorsoFrame.AngleToArena(0f, torso, Side.Left), Is.EqualTo(90f).Within(Tolerance));
        }

        [TestCase(Side.Left)]
        [TestCase(Side.Right)]
        public void ToLocal_InvertsToArena(Side side)
        {
            var torso = new BodyPose(new Vec2(120f, -40f), 25f);
            var local = new Vec2(310f, 95f);

            AssertNear(TorsoFrame.ToLocal(TorsoFrame.ToArena(local, torso, side), torso, side), local);
            Assert.That(XMath.DeltaAngleDegrees(40f, TorsoFrame.AngleToLocal(TorsoFrame.AngleToArena(40f, torso, side), torso, side)),
                Is.EqualTo(0f).Within(Tolerance));
        }

        [TestCase(Side.Left)]
        [TestCase(Side.Right)]
        public void PoseToLocal_InvertsPoseToArena(Side side)
        {
            var torso = new BodyPose(new Vec2(120f, -40f), 25f);
            var local = new BodyPose(new Vec2(310f, 95f), -35f);

            BodyPose back = TorsoFrame.ToLocal(TorsoFrame.ToArena(local, torso, side), torso, side);

            AssertNear(back.PositionUnits, local.PositionUnits);
            Assert.That(XMath.DeltaAngleDegrees(local.RotationDegrees, back.RotationDegrees), Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void TurnStartRoot_IsTheFrozenPelvisStandingUpright()
        {
            var pose = new FighterPose();
            pose.Set(BodyPart.Torso, new BodyPose(new Vec2(-350f, 175f), 12f));

            BodyPose root = TurnStartRoot.Of(pose);

            Assert.That(root.PositionUnits, Is.EqualTo(new Vec2(-350f, 175f)));
            Assert.That(root.RotationDegrees, Is.EqualTo(0f));
        }
    }
}
