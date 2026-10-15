using System.Linq;
using NUnit.Framework;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Settings;
using XRim.Simulation.Physics;
using XRim.Simulation.Settings;

namespace XRim.Tests.EditMode.Simulation.Physics
{
    /// <summary>
    /// The dummy's layout (Session 13: the art spec and the hit-zone overlay read it): pelvis at the origin, torso 60 × 120 rising, head
    /// Ø 50 on the neck at 120, arms 24 × 240 hanging from the shoulder at 100, legs 28 × 180 from the pelvis; ten bodies split each limb
    /// in half. A PlayMode test holds the builder to the same numbers.
    /// </summary>
    public sealed class DummyLayoutTests
    {
        private const float Tolerance = 1e-4f;

        private readonly RagdollSettings _body = new RagdollSettings();
        private readonly PathSettings _paths = new PathSettings();

        private DummyLayout Six => DummyLayout.Of(_body, _paths, RagdollSegmentation.SixBodies);
        private DummyLayout Ten => DummyLayout.Of(_body, _paths, RagdollSegmentation.TenBodies);

        private static void AssertPoint(Vec2 actual, float x, float y, string what)
        {
            Assert.That(actual.X, Is.EqualTo(x).Within(Tolerance), what + " x");
            Assert.That(actual.Y, Is.EqualTo(y).Within(Tolerance), what + " y");
        }

        [Test]
        public void SixBodies_AreTheTorsoHeadArmsAndLegs_PlusTheTorsoHalves()
        {
            Assert.That(Six.Bodies.Select(b => b.Name), Is.EqualTo(new[]
                { "Torso", "TorsoBottom", "TorsoTop", "Head", "LeftArm", "RightArm", "LeftLeg", "RightLeg" }));
            Assert.That(Six.Bodies.Where(b => b.OnlyAfterDeath).Select(b => b.Name), Is.EqualTo(new[] { "TorsoBottom", "TorsoTop" }));
        }

        [Test]
        public void TenBodies_SplitEachLimbIntoUpperAndLower()
        {
            Assert.That(Ten.Bodies.Count, Is.EqualTo(12), "ten bodies and the two torso halves");
            foreach (BodyPart limb in new[] { BodyPart.LeftArm, BodyPart.RightArm, BodyPart.LeftLeg, BodyPart.RightLeg })
            {
                Assert.That(Ten.BodiesOf(limb).Select(b => b.Name), Is.EqualTo(new[] { limb + "Upper", limb + "Lower" }));
            }
        }

        [Test]
        public void TheTorso_RisesFromThePelvis_AndSplitsAtHalfItsHeight()
        {
            DummyLayout layout = Six;
            DummyBodyLayout torso = layout.Find("Torso");
            AssertPoint(torso.PivotUnits, 0f, 0f, "pelvis");
            AssertPoint(torso.SizeUnits, 60f, 120f, "size");
            AssertPoint(torso.CentreUnits, 0f, 60f, "centre");
            AssertPoint(layout.Find("TorsoTop").PivotUnits, 0f, 60f, "split line");
            Assert.That(layout.Find("TorsoBottom").SizeUnits.Y + layout.Find("TorsoTop").SizeUnits.Y, Is.EqualTo(120f).Within(Tolerance));
        }

        [Test]
        public void TheHead_IsADiscOnTheNeck()
        {
            DummyBodyLayout head = Six.Find("Head");
            Assert.That(head.IsRound, Is.True);
            AssertPoint(head.PivotUnits, 0f, 120f, "neck");
            AssertPoint(head.CentreUnits, 0f, 145f, "centre");
            Assert.That(head.MarkerDiameterUnits, Is.EqualTo(40f).Within(Tolerance), "0.8 of Ø 50");
        }

        [Test]
        public void TenBodyArms_HangFromTheShoulder_WithTheElbowHalfwayDown()
        {
            DummyLayout layout = Ten;
            DummyBodyLayout upper = layout.Find("RightArmUpper");
            DummyBodyLayout lower = layout.Find("RightArmLower");
            AssertPoint(upper.PivotUnits, 0f, 100f, "shoulder");
            AssertPoint(upper.SizeUnits, 24f, 120f, "upper arm");
            AssertPoint(lower.PivotUnits, 0f, -20f, "elbow");
            AssertPoint(lower.PivotUnits + lower.EndLocalUnits, 0f, -140f, "hand");
            AssertPoint(layout.RestGripUnits, 0f, -140f, "the grip rests at the hand");
        }

        [Test]
        public void Joints_JoinEachChildAtItsPivot_WithTheSettingsLimits()
        {
            DummyLayout six = Six;
            DummyLayout ten = Ten;
            Assert.That(six.Joints.Select(j => j.Name), Is.EqualTo(new[] { "Neck", "LeftShoulder", "RightShoulder", "LeftHip", "RightHip" }));
            Assert.That(ten.Joints.Count, Is.EqualTo(9), "the neck and two per limb");
            foreach (DummyJointLayout joint in ten.Joints)
            {
                DummyBodyLayout child = ten.Find(joint.ChildBody);
                Assert.That(child, Is.Not.Null, joint.Name);
                Assert.That(ten.Find(joint.ParentBody), Is.Not.Null, joint.Name);
                AssertPoint(joint.AnchorUnits, child.PivotUnits.X, child.PivotUnits.Y, joint.Name);
            }

            DummyJointLayout knee = ten.Joints.Single(j => j.Name == "LeftKnee");
            Assert.That((knee.ChildBody, knee.ParentBody), Is.EqualTo(("LeftLegLower", "LeftLegUpper")));
            Assert.That((knee.MinDegrees, knee.MaxDegrees), Is.EqualTo((_body.KneeMinDegrees, _body.KneeMaxDegrees)));
            AssertPoint(knee.AnchorUnits, 0f, -90f, "knee halfway down the leg");
        }

        [Test]
        public void NearLimbs_DrawInFrontOfTheTorso_FarLimbsBehind()
        {
            DummyLayout layout = Ten;
            int torso = layout.Find("Torso").SortingOrder;
            Assert.That(layout.Find("Head").SortingOrder, Is.GreaterThan(torso));
            Assert.That(layout.BodiesOf(BodyPart.RightArm).Concat(layout.BodiesOf(BodyPart.RightLeg)).Select(b => b.SortingOrder),
                Has.All.GreaterThan(torso));
            Assert.That(layout.BodiesOf(BodyPart.LeftArm).Concat(layout.BodiesOf(BodyPart.LeftLeg)).Select(b => b.SortingOrder),
                Has.All.LessThan(torso));
            Assert.That(DummyLayout.HeldItemOrder, Is.GreaterThan(DummyLayout.NearLimbOrder), "the weapon draws over everything");
        }

        [Test]
        public void TheLayout_FollowsTheSettings()
        {
            var body = new RagdollSettings { TorsoHeightUnits = 150f, UpperSegmentFraction = 0.4f };
            var paths = new PathSettings { ArmLengthUnits = 300f };

            DummyLayout layout = DummyLayout.Of(body, paths, RagdollSegmentation.TenBodies);

            AssertPoint(layout.Find("Head").PivotUnits, 0f, 150f, "the neck moves with the torso");
            Assert.That(layout.Find("LeftArmUpper").SizeUnits.Y, Is.EqualTo(120f).Within(Tolerance), "0.4 of 300");
            Assert.That(layout.Find("LeftArmLower").SizeUnits.Y, Is.EqualTo(180f).Within(Tolerance));
        }
    }
}
