using System;
using NUnit.Framework;
using XRim.Core;
using XRim.Networking.Protocol;
using XRim.Rules;
using XRim.Simulation.Physics;
using XRim.Simulation.Recording;
using XRim.Tests.EditMode.Simulation;

namespace XRim.Tests.EditMode.Networking.Protocol
{
    /// <summary>Recorded poses travel rounded to a grid far finer than a pixel, and nothing but positions and rotations changes.</summary>
    public sealed class PoseRoundingTests
    {
        [TestCase(0f)]
        [TestCase(0.03124f)]
        [TestCase(-0.03126f)]
        [TestCase(123.456789f)]
        [TestCase(-987.654321f)]
        [TestCase(10000.123f)]
        public void APosition_MovesAtMostHalfAStep(float value)
        {
            var pose = new BodyPose(new Vec2(value, -value), value);

            BodyPose rounded = PoseRounding.Round(pose);

            Assert.That(Math.Abs(rounded.PositionUnits.X - value), Is.LessThanOrEqualTo(PoseRounding.PositionStepUnits / 2f));
            Assert.That(Math.Abs(rounded.PositionUnits.Y + value), Is.LessThanOrEqualTo(PoseRounding.PositionStepUnits / 2f));
            Assert.That(Math.Abs(rounded.RotationDegrees - value), Is.LessThanOrEqualTo(PoseRounding.RotationStepDegrees / 2f));
        }

        [Test]
        public void RoundingTwice_ChangesNothingMore()
        {
            PoseSnapshot once = PoseRounding.Round(Frame());

            PoseSnapshot twice = PoseRounding.Round(once);

            Assert.That(TurnFingerprint.OfValue(twice), Is.EqualTo(TurnFingerprint.OfValue(once)));
        }

        [Test]
        public void EverythingButPositionsAndRotations_IsUnchanged()
        {
            PoseSnapshot pose = Frame();

            PoseSnapshot rounded = PoseRounding.Round(pose);

            foreach (Side side in new[] { Side.Left, Side.Right })
            {
                FighterPose before = pose.Get(side);
                FighterPose after = rounded.Get(side);
                Assert.That(after.HasLowerSegments, Is.EqualTo(before.HasLowerSegments));
                Assert.That(after.HasHeldItem, Is.EqualTo(before.HasHeldItem));
                Assert.That(after.SeveredParts, Is.EqualTo(before.SeveredParts));
                Assert.That(after.IsTorsoSplit, Is.EqualTo(before.IsTorsoSplit));
            }

            Assert.That(rounded.SeveredLimbs.Count, Is.EqualTo(pose.SeveredLimbs.Count));
            Assert.That(rounded.SeveredLimbs[0].Part, Is.EqualTo(pose.SeveredLimbs[0].Part));
            Assert.That(rounded.LeftWall.Phase, Is.EqualTo(pose.LeftWall.Phase));
        }

        [Test]
        public void RoundingLeavesTheOriginalAlone()
        {
            PoseSnapshot pose = Frame();
            string before = TurnFingerprint.OfValue(pose);

            PoseRounding.Round(pose);

            Assert.That(TurnFingerprint.OfValue(pose), Is.EqualTo(before));
        }

        [Test]
        public void AValueThatIsNotANumber_IsRefused()
        {
            var pose = new BodyPose(new Vec2(float.NaN, 0f), 0f);

            Assert.Throws<ArgumentException>(() => PoseRounding.Round(pose));
        }

        private static PoseSnapshot Frame()
        {
            TurnResult turn = ProtocolTestData.FullLengthTurn();
            PoseSnapshot pose = turn.Timeline.Frames[turn.Timeline.Frames.Count - 1].Pose.Clone();
            pose.Right.IsTorsoSplit = true;
            pose.Right.MarkSevered(BodyPart.LeftArm);
            return pose;
        }
    }
}
