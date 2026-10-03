using System;
using NUnit.Framework;
using XRim.Core;
using XRim.Simulation.Physics;
using XRim.Simulation.Settings;

namespace XRim.Tests.EditMode.Simulation.Physics
{
    /// <summary>
    /// The leg angles that keep a dummy on its feet during a body move (GDD §5). Each result is checked by building the leg
    /// back from the angles (forward kinematics): the end of the leg is a flat box end, so the point that must land on the
    /// target is its lowest corner.
    /// </summary>
    public sealed class LegPoserTests
    {
        private const float Tolerance = 0.1f;
        private const float AngleTolerance = 0.01f;

        private RagdollSettings _body;

        [SetUp]
        public void SetUp() => _body = new RagdollSettings();

        private float Thigh => _body.LegLengthUnits * _body.UpperSegmentFraction;
        private float Shin => _body.LegLengthUnits - Thigh;

        /// <summary>A limb hanging at an angle from straight down (positive toward the opponent), in the fighter's frame.</summary>
        private static Vec2 Hanging(float degrees)
        {
            double radians = degrees * XMath.DegreesToRadians;
            return new Vec2((float)Math.Sin(radians), (float)-Math.Cos(radians));
        }

        private static Vec2 ToArena(Vec2 fighterFrame, Side side) => new Vec2(fighterFrame.X * side.FacingSign(), fighterFrame.Y);

        private static float ThighDegrees(BodyPose pelvis, Side side, LimbAngles angles) => side.FacingSign() * pelvis.RotationDegrees + angles.UpperDegrees;

        private Vec2 Knee(BodyPose pelvis, Side side, LimbAngles angles) =>
            pelvis.PositionUnits + ToArena(Hanging(ThighDegrees(pelvis, side, angles)) * Thigh, side);

        /// <summary>The centre of the leg's end, and the angle of its last segment from straight down.</summary>
        private Vec2 End(BodyPose pelvis, Side side, LimbAngles angles, RagdollSegmentation segmentation, out float lastDegrees)
        {
            float thighDegrees = ThighDegrees(pelvis, side, angles);
            if (segmentation == RagdollSegmentation.SixBodies)
            {
                lastDegrees = thighDegrees;
                return pelvis.PositionUnits + ToArena(Hanging(thighDegrees) * _body.LegLengthUnits, side);
            }

            lastDegrees = thighDegrees + angles.LowerDegrees;
            return Knee(pelvis, side, angles) + ToArena(Hanging(lastDegrees) * Shin, side);
        }

        /// <summary>The foot's lowest point: the end's centre x, at the height of its lower corner.</summary>
        private Vec2 Foot(BodyPose pelvis, Side side, LimbAngles angles, RagdollSegmentation segmentation = RagdollSegmentation.TenBodies)
        {
            Vec2 end = End(pelvis, side, angles, segmentation, out float lastDegrees);
            float drop = _body.LegWidthUnits * 0.5f * Math.Abs((float)Math.Sin(lastDegrees * XMath.DegreesToRadians));
            return new Vec2(end.X, end.Y - drop);
        }

        private LimbAngles Solve(BodyPose pelvis, Vec2 target, Side side, bool front = true,
            RagdollSegmentation segmentation = RagdollSegmentation.TenBodies) =>
            LegPoser.Solve(pelvis, target, side, front, segmentation, _body);

        private static void AssertNear(Vec2 actual, Vec2 expected, string what)
        {
            Assert.That(actual.X, Is.EqualTo(expected.X).Within(Tolerance), what + " x");
            Assert.That(actual.Y, Is.EqualTo(expected.Y).Within(Tolerance), what + " y");
        }

        [Test]
        public void StandingStraight_IsTheRestPose()
        {
            var pelvis = new BodyPose(new Vec2(-350f, _body.LegLengthUnits), 0f);

            LimbAngles angles = Solve(pelvis, new Vec2(-350f, 0f), Side.Left);

            Assert.That(angles.UpperDegrees, Is.EqualTo(0f).Within(AngleTolerance));
            Assert.That(angles.LowerDegrees, Is.EqualTo(0f).Within(AngleTolerance));
        }

        [Test]
        public void Crouch_BendsTheKneeTowardTheOpponent_AndPutsTheFootOnTarget([Values(Side.Left, Side.Right)] Side side)
        {
            float facing = side.FacingSign();
            var pelvis = new BodyPose(new Vec2(-350f * facing, 110f), 0f);
            var target = new Vec2(pelvis.PositionUnits.X + 20f * facing, 0f);

            LimbAngles angles = Solve(pelvis, target, side);

            AssertNear(Foot(pelvis, side, angles), target, "foot");
            Assert.That(angles.LowerDegrees, Is.LessThan(0f), "the knee bends backward");
            Assert.That((Knee(pelvis, side, angles).X - pelvis.PositionUnits.X) * facing, Is.GreaterThan(0f),
                "the knee points toward the opponent");
        }

        [Test]
        public void TiltedShin_RestsOnItsCorner_NotSunkIntoTheFloor()
        {
            var pelvis = new BodyPose(new Vec2(0f, 110f), 0f);

            LimbAngles angles = Solve(pelvis, new Vec2(20f, 0f), Side.Left);
            Vec2 end = End(pelvis, Side.Left, angles, RagdollSegmentation.TenBodies, out float shinDegrees);

            Assert.That(Math.Abs(shinDegrees), Is.GreaterThan(10f), "the shin is tilted in a crouch");
            Assert.That(end.Y, Is.GreaterThan(1f), "the end's centre sits above the floor, by the corner's drop");
        }

        [Test]
        public void TorsoLean_IsTakenUpAtTheHip()
        {
            var upright = new BodyPose(new Vec2(0f, 120f), 0f);
            var leaning = new BodyPose(new Vec2(0f, 120f), -10f);
            var target = new Vec2(30f, 0f);

            LimbAngles straight = Solve(upright, target, Side.Left);
            LimbAngles leaned = Solve(leaning, target, Side.Left);

            AssertNear(Foot(leaning, Side.Left, leaned), target, "the foot stays put");
            Assert.That(leaned.UpperDegrees, Is.EqualTo(straight.UpperDegrees + 10f).Within(AngleTolerance),
                "leaning forward 10° (clockwise for the left dummy) bends the hip 10° more");
        }

        [Test]
        public void Stride_FrontAndBackFeetLandOnTheFloor()
        {
            var pelvis = new BodyPose(new Vec2(100f, 160f), 0f);
            var front = new Vec2(170f, 0f);
            var back = new Vec2(30f, 0f);

            AssertNear(Foot(pelvis, Side.Left, Solve(pelvis, front, Side.Left)), front, "front foot");
            AssertNear(Foot(pelvis, Side.Left, Solve(pelvis, back, Side.Left, false)), back, "back foot");
        }

        [Test]
        public void Hop_TucksTheKnees_SoTheFeetRise()
        {
            var pelvis = new BodyPose(new Vec2(0f, 260f), 0f);
            var tucked = new Vec2(0f, 100f);

            LimbAngles angles = Solve(pelvis, tucked, Side.Right);

            AssertNear(Foot(pelvis, Side.Right, angles), tucked, "foot");
        }

        [Test]
        public void TargetOutOfReach_PointsTheStraightLegAtIt()
        {
            var pelvis = new BodyPose(new Vec2(0f, 180f), 0f);

            LimbAngles below = Solve(pelvis, new Vec2(0f, -50f), Side.Left);
            LimbAngles ahead = Solve(pelvis, new Vec2(100f, 0f), Side.Left);

            Assert.That(below.UpperDegrees, Is.EqualTo(0f).Within(AngleTolerance));
            Assert.That(below.LowerDegrees, Is.EqualTo(0f).Within(AngleTolerance), "straight");
            Assert.That(ahead.LowerDegrees, Is.EqualTo(0f).Within(AngleTolerance), "straight");
            float straightAt = (float)(Math.Atan2(100.0, 180.0) * XMath.RadiansToDegrees);
            Assert.That(ahead.UpperDegrees, Is.InRange(straightAt, straightAt + 3f), "toward the target, its corner raised a little");
        }

        [Test]
        public void Angles_StayWithinTheJointLimits()
        {
            var pelvis = new BodyPose(new Vec2(0f, 100f), 0f);

            LimbAngles farBehind = Solve(pelvis, new Vec2(-170f, 0f), Side.Left, false);
            LimbAngles folded = Solve(new BodyPose(new Vec2(0f, 20f), 0f), new Vec2(0f, 0f), Side.Left);

            Assert.That(farBehind.UpperDegrees, Is.GreaterThanOrEqualTo(_body.HipMinDegrees));
            Assert.That(folded.UpperDegrees, Is.LessThanOrEqualTo(_body.HipMaxDegrees));
            Assert.That(folded.LowerDegrees, Is.GreaterThanOrEqualTo(_body.KneeMinDegrees));
        }

        [Test]
        public void SixBodies_StraightLegsSwingUntilTheFeetMeetTheFloor()
        {
            var pelvis = new BodyPose(new Vec2(0f, 165f), 0f);
            var under = new Vec2(0f, 0f);

            LimbAngles front = Solve(pelvis, under, Side.Left, true, RagdollSegmentation.SixBodies);
            LimbAngles back = Solve(pelvis, under, Side.Left, false, RagdollSegmentation.SixBodies);

            Assert.That(front.UpperDegrees, Is.GreaterThan(0f), "the front leg swings forward");
            Assert.That(back.UpperDegrees, Is.LessThan(0f), "the back leg swings back");
            Assert.That(Foot(pelvis, Side.Left, front, RagdollSegmentation.SixBodies).Y, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(Foot(pelvis, Side.Left, back, RagdollSegmentation.SixBodies).Y, Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void SixBodies_BackLegSwingsForward_WhenTheHipCannotSwingBackFarEnough()
        {
            var pelvis = new BodyPose(new Vec2(0f, 110f), 0f);

            LimbAngles back = Solve(pelvis, new Vec2(0f, 0f), Side.Left, false, RagdollSegmentation.SixBodies);

            Assert.That(back.UpperDegrees, Is.GreaterThan(0f), "a deep crouch puts both straight legs in front");
            Assert.That(Foot(pelvis, Side.Left, back, RagdollSegmentation.SixBodies).Y, Is.EqualTo(0f).Within(Tolerance),
                "never under the floor");
        }
    }
}
