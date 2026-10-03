using NUnit.Framework;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Settings;
using XRim.Simulation.Drivers;
using XRim.Simulation.Physics;
using XRim.Simulation.Settings;

namespace XRim.Tests.EditMode.Simulation.Physics
{
    /// <summary>Where the feet and the floor are, read from a pose laid out like the placeholder ragdoll.</summary>
    public sealed class LegGeometryTests
    {
        private const float Tolerance = 1e-3f;

        private readonly RagdollSettings _body = new RagdollSettings();
        private readonly PathSettings _paths = new PathSettings();
        private readonly WeaponStats _rapier = GddStartingValues.Rapier();

        private FighterPose GuardPose(Side side, float x, RagdollSegmentation segmentation) => GuardStance.Create(
            new BodyPose(new Vec2(x, LegGeometry.StandingPelvisHeightUnits(_body)), 0f), side, BodyPart.RightArm, _rapier, segmentation,
            _body, _paths, new AimFromShoulderModel());

        [Test]
        public void StandingDummy_HasItsSolesOnTheFloorUnderThePelvis(
            [Values(RagdollSegmentation.SixBodies, RagdollSegmentation.TenBodies)] RagdollSegmentation segmentation,
            [Values(Side.Left, Side.Right)] Side side)
        {
            FighterPose pose = GuardPose(side, 350f * -side.FacingSign(), segmentation);
            float pelvisX = pose.Get(BodyPart.Torso).PositionUnits.X;

            foreach (BodyPart leg in new[] { BodyPart.LeftLeg, BodyPart.RightLeg })
            {
                Vec2 sole = LegGeometry.SoleOf(pose, leg, _body);
                Assert.That(sole.X, Is.EqualTo(pelvisX).Within(Tolerance), $"{leg} x");
                Assert.That(sole.Y, Is.EqualTo(LegGeometry.FloorYUnits).Within(Tolerance), $"{leg} y");
            }
        }

        [Test]
        public void SoleOf_FollowsATiltedShin()
        {
            FighterPose pose = GuardPose(Side.Left, 0f, RagdollSegmentation.TenBodies);
            BodyPose shin = pose.GetLower(BodyPart.RightLeg);
            pose.SetLower(BodyPart.RightLeg, new BodyPose(shin.PositionUnits, 90f));

            Vec2 sole = LegGeometry.SoleOf(pose, BodyPart.RightLeg, _body);

            Assert.That(sole.X, Is.EqualTo(shin.PositionUnits.X + _body.LegLengthUnits * (1f - _body.UpperSegmentFraction)).Within(Tolerance),
                "a shin turned 90° counter-clockwise points its sole along +X");
            Assert.That(sole.Y, Is.EqualTo(shin.PositionUnits.Y).Within(Tolerance));
        }

        [Test]
        public void FrontLeg_IsOnTheDominantSide()
        {
            Assert.That(LegGeometry.FrontLeg(Handedness.Right), Is.EqualTo(BodyPart.RightLeg));
            Assert.That(LegGeometry.BackLeg(Handedness.Right), Is.EqualTo(BodyPart.LeftLeg));
            Assert.That(LegGeometry.FrontLeg(Handedness.Left), Is.EqualTo(BodyPart.LeftLeg));
        }

        [Test]
        public void BodyMoveStart_ReadsTheFrozenRootAndSoles()
        {
            FighterPose pose = GuardPose(Side.Right, 350f, RagdollSegmentation.TenBodies);
            pose.Set(BodyPart.Torso, new BodyPose(pose.Get(BodyPart.Torso).PositionUnits, 12f));

            BodyMoveStart start = BodyMoveStart.Of(Side.Right, pose, Handedness.Right, _body);

            Assert.That(start.Root.PositionUnits, Is.EqualTo(new Vec2(350f, _body.LegLengthUnits)));
            Assert.That(start.Root.RotationDegrees, Is.EqualTo(TurnStartRoot.UprightDegrees), "a turn starts upright");
            Assert.That(start.FrontSoleUnits.X, Is.EqualTo(350f).Within(Tolerance));
            Assert.That(start.StandingHeightUnits, Is.EqualTo(_body.LegLengthUnits));
        }
    }
}
