using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using XRim.Config;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Settings;
using XRim.Simulation.Drivers;
using XRim.Simulation.Physics;
using XRim.Simulation.Settings;
using XRim.Simulation.Unity2D;

namespace XRim.Tests.PlayMode.Simulation
{
    /// <summary>The placeholder dummy built in code (Session 02): bodies, joints, reach, held items, mirroring.</summary>
    public sealed class PlaceholderRagdollTests
    {
        private const float Tolerance = 1e-3f;

        private readonly List<GameObject> _created = new List<GameObject>();
        private RagdollSettings _body;
        private PathSettings _paths;
        private ArenaSpace _space;

        [SetUp]
        public void SetUp()
        {
            _body = new RagdollSettings();
            _paths = new PathSettings();
            _space = new ArenaSpace(ArenaSpaceConfig.DefaultWorldUnitsPerArenaUnit);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject created in _created)
            {
                Object.DestroyImmediate(created);
            }

            _created.Clear();
        }

        private Ragdoll Build(RagdollSegmentation segmentation)
        {
            var weapons = new List<WeaponStats> { GddStartingValues.Rapier(), GddStartingValues.Mace() };
            Ragdoll ragdoll = PlaceholderRagdollBuilder.Build(new RagdollBuildSpec(_body, _paths, segmentation, _space, weapons, null));
            _created.Add(ragdoll.gameObject);
            return ragdoll;
        }

        private static int CountJoints(Ragdoll ragdoll) => ragdoll.GetComponentsInChildren<HingeJoint2D>(true).Length;

        [Test]
        public void SixBodies_OneBodyPerPartAndFiveJoints()
        {
            Ragdoll ragdoll = Build(RagdollSegmentation.SixBodies);
            var bodies = new List<Rigidbody2D>();
            ragdoll.GetPartBodies(bodies);

            Assert.That(bodies.Count, Is.EqualTo(6));
            Assert.That(CountJoints(ragdoll), Is.EqualTo(5));
            Assert.That(ragdoll.HasLowerSegments, Is.False);
            foreach (BodyPart part in BodyParts.All)
            {
                Assert.That(ragdoll.GetBody(part).GetComponent<PhysicsBodyTag>().Part, Is.EqualTo(part));
            }
        }

        [Test]
        public void TenBodies_SplitLimbsIntoTwoSegmentsOfTheSameZone()
        {
            Ragdoll ragdoll = Build(RagdollSegmentation.TenBodies);
            var bodies = new List<Rigidbody2D>();
            ragdoll.GetPartBodies(bodies);

            Assert.That(bodies.Count, Is.EqualTo(10));
            Assert.That(CountJoints(ragdoll), Is.EqualTo(9));
            Assert.That(ragdoll.GetLowerBody(BodyPart.LeftLeg).GetComponent<PhysicsBodyTag>().Part, Is.EqualTo(BodyPart.LeftLeg));
            Assert.That(ragdoll.GetLowerBody(BodyPart.Head), Is.Null);
        }

        [TestCase(RagdollSegmentation.SixBodies)]
        [TestCase(RagdollSegmentation.TenBodies)]
        public void Hand_IsOneArmLengthBelowTheShoulder(RagdollSegmentation segmentation)
        {
            Ragdoll ragdoll = Build(segmentation);
            Vector2 hand = ragdoll.GetHand(BodyPart.RightArm).position;

            Vec2 handUnits = _space.ToArena(hand);
            Assert.That(handUnits.X, Is.EqualTo(_paths.ShoulderOffsetUnits.X).Within(Tolerance));
            Assert.That(handUnits.Y, Is.EqualTo(_paths.ShoulderOffsetUnits.Y - _paths.ArmLengthUnits).Within(Tolerance));
            Assert.That(ragdoll.MatchesReach(_paths, _space), Is.True);
        }

        [Test]
        public void HeldItems_ArePerWeaponSizedFromStatsAndInactive()
        {
            Ragdoll ragdoll = Build(RagdollSegmentation.SixBodies);
            WeaponStats rapier = GddStartingValues.Rapier();
            Rigidbody2D item = ragdoll.FindHeldItem(rapier.Id);

            Assert.That(ragdoll.HeldItems.Count, Is.EqualTo(2));
            Assert.That(item, Is.Not.Null);
            Assert.That(item.gameObject.activeSelf, Is.False);
            Assert.That(item.mass, Is.EqualTo(rapier.Mass).Within(Tolerance));
            Vector2 size = item.GetComponent<BoxCollider2D>().size;
            Assert.That(size.x, Is.EqualTo(_space.ToWorldLength(rapier.LengthUnits)).Within(Tolerance));
            Assert.That(size.y, Is.EqualTo(_space.ToWorldLength(rapier.InkThicknessUnits)).Within(Tolerance), "ink thickness is the hit width");
            Assert.That(item.collisionDetectionMode, Is.EqualTo(CollisionDetectionMode2D.Continuous));
        }

        [Test]
        public void RestPose_RightSideFacesMinusX()
        {
            Ragdoll ragdoll = Build(RagdollSegmentation.SixBodies);
            var torso = new BodyPose(new Vec2(350f, 180f), 0f);
            WeaponStats rapier = GddStartingValues.Rapier();
            var aim = new AimFromShoulderModel();

            FighterPose left = ragdoll.CreateRestPose(new BodyPose(new Vec2(-350f, 180f), 0f), Side.Left, _space, BodyPart.RightArm,
                rapier, _body, aim);
            FighterPose right = ragdoll.CreateRestPose(torso, Side.Right, _space, BodyPart.RightArm, rapier, _body, aim);

            Assert.That(right.Get(BodyPart.Torso).PositionUnits, Is.EqualTo(torso.PositionUnits));
            Assert.That(right.Get(BodyPart.Head).PositionUnits.Y, Is.EqualTo(180f + _body.TorsoHeightUnits).Within(Tolerance));
            Assert.That(XMath.DeltaAngleDegrees(_body.GuardAngleDegrees, left.HeldItem.RotationDegrees), Is.EqualTo(0f).Within(Tolerance),
                "en garde: the weapon points at the guard angle");
            Assert.That(XMath.DeltaAngleDegrees(180f - _body.GuardAngleDegrees, right.HeldItem.RotationDegrees), Is.EqualTo(0f).Within(Tolerance),
                "mirrored for the right-hand fighter");
        }

        [TestCase(RagdollSegmentation.SixBodies)]
        [TestCase(RagdollSegmentation.TenBodies)]
        public void RestPose_WeaponArmReachesTheBlade(RagdollSegmentation segmentation)
        {
            Ragdoll ragdoll = Build(segmentation);
            WeaponStats rapier = GddStartingValues.Rapier();
            FighterPose pose = ragdoll.CreateRestPose(new BodyPose(Vec2.Zero, 0f), Side.Left, _space, BodyPart.RightArm, rapier, _body,
                new AimFromShoulderModel());

            // The hand is the end of the arm's last segment; it must lie on the blade's centre line.
            BodyPose last = segmentation == RagdollSegmentation.TenBodies ? pose.GetLower(BodyPart.RightArm) : pose.Get(BodyPart.RightArm);
            float lastLength = _space.ToArenaLength(ragdoll.GetHand(BodyPart.RightArm).localPosition.magnitude);
            Vec2 hand = last.PositionUnits + Vec2.FromAngleDegrees(last.RotationDegrees - 90f) * lastLength;
            Vec2 axis = Vec2.FromAngleDegrees(pose.HeldItem.RotationDegrees);
            Vec2 fromGrip = hand - pose.HeldItem.PositionUnits;
            float offLine = Mathf.Abs(Vec2.Cross(axis, fromGrip));

            Assert.That(offLine, Is.LessThan(0.5f), "the hand holds the blade, so arm and weapon do not fight");
        }

        [Test]
        public void ApplyTuning_SetsMassesAndFlipsLimitsWhenMirrored()
        {
            Ragdoll ragdoll = Build(RagdollSegmentation.TenBodies);
            ragdoll.MirrorForRightSide();
            _body.ArmMass = 3f;

            ragdoll.ApplyTuning(_body, 1f, BodyPart.RightArm);

            float upperArm = ragdoll.GetBody(BodyPart.LeftArm).mass;
            float lowerArm = ragdoll.GetLowerBody(BodyPart.LeftArm).mass;
            Assert.That(upperArm + lowerArm, Is.EqualTo(3f).Within(Tolerance));
            HingeJoint2D knee = ragdoll.GetLowerBody(BodyPart.LeftLeg).GetComponent<HingeJoint2D>();
            Assert.That(knee.limits.min, Is.EqualTo(-_body.KneeMaxDegrees).Within(Tolerance));
            Assert.That(knee.limits.max, Is.EqualTo(-_body.KneeMinDegrees).Within(Tolerance));
            Assert.That(ragdoll.GetBody(BodyPart.RightArm).GetComponent<HingeJoint2D>().useMotor, Is.False, "the weapon arm is carried, not powered");
            Assert.That(ragdoll.GetBody(BodyPart.LeftArm).GetComponent<HingeJoint2D>().useMotor, Is.True);
        }
    }
}
