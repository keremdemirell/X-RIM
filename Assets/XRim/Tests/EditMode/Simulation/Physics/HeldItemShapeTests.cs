using NUnit.Framework;
using XRim.Core;
using XRim.Rules.Settings;
using XRim.Simulation.Execution;
using XRim.Simulation.Physics;

namespace XRim.Tests.EditMode.Simulation.Physics
{
    /// <summary>
    /// How each held item sits on its body (GDD §6; A3 for the shield), the one layout the physics box, the drawing, the
    /// time-to-impact sweep and the path's start all share. Rapier 400 × 10; shield 150 tall, 40 thick.
    /// </summary>
    public sealed class HeldItemShapeTests
    {
        private const float Tolerance = 1e-3f;

        private readonly WeaponStats _rapier = GddStartingValues.Rapier();
        private readonly WeaponStats _shield = GddStartingValues.Shield();

        [Test]
        public void AWeapon_RunsFromTheGripToItsTip_AndItsTipFollowsThePath()
        {
            HeldItemShape shape = HeldItemShape.Of(_rapier);

            Assert.That(shape.IsShield, Is.False);
            Assert.That(shape.CentreLocal, Is.EqualTo(new Vec2(200f, 0f)));
            Assert.That(shape.SizeUnits, Is.EqualTo(new Vec2(400f, 10f)));
            Assert.That(shape.PathPointLocal, Is.EqualTo(new Vec2(400f, 0f)));
            Assert.That(shape.CentreLineStartLocal, Is.EqualTo(Vec2.Zero));
            Assert.That(shape.CentreLineEndLocal, Is.EqualTo(new Vec2(400f, 0f)));
            Assert.That(shape.HalfThicknessUnits, Is.EqualTo(5f));
        }

        [Test]
        public void TheShield_IsCentredOnTheGrip_FaceOutward_AndItsCentreFollowsThePath()
        {
            HeldItemShape shape = HeldItemShape.Of(_shield);

            Assert.That(shape.IsShield, Is.True);
            Assert.That(shape.CentreLocal, Is.EqualTo(Vec2.Zero));
            Assert.That(shape.SizeUnits, Is.EqualTo(new Vec2(40f, 150f)), "thickness along the way it faces, height across");
            Assert.That(shape.PathPointLocal, Is.EqualTo(Vec2.Zero));
            Assert.That(shape.CentreLineStartLocal, Is.EqualTo(new Vec2(0f, -75f)));
            Assert.That(shape.CentreLineEndLocal, Is.EqualTo(new Vec2(0f, 75f)));
            Assert.That(shape.HalfThicknessUnits, Is.EqualTo(20f));
        }

        [Test]
        public void ASeveredLimbClub_IsShapedLikeAWeapon()
        {
            HeldItemShape shape = HeldItemShape.Of(GddStartingValues.SeveredLimb());

            Assert.That(shape.IsShield, Is.False);
            Assert.That(shape.PathPointLocal.X, Is.EqualTo(GddStartingValues.SeveredLimb().LengthUnits));
        }

        [Test]
        public void ThePathPoint_TurnsWithTheBody()
        {
            var body = new BodyPose(new Vec2(10f, 20f), 90f);

            Vec2 tip = HeldItemShape.Of(_rapier).PathPointAt(body);

            Assert.That(tip.X, Is.EqualTo(10f).Within(Tolerance));
            Assert.That(tip.Y, Is.EqualTo(420f).Within(Tolerance));
            Assert.That(HeldItemShape.Of(_shield).PathPointAt(body), Is.EqualTo(body.PositionUnits));
        }

        [Test]
        public void TheSweptShape_OfAWeapon_IsTheBladeFromGripToTip()
        {
            BladeShape swept = BladeShape.Of(_rapier);
            var blade = new BladeShape(400f, 10f);
            var pose = new BodyPose(new Vec2(5f, 5f), 30f);

            Assert.That(swept.HalfWidthUnits, Is.EqualTo(5f));
            Assert.That(swept.LengthUnits, Is.EqualTo(400f).Within(Tolerance));
            foreach (Vec2 point in new[] { new Vec2(100f, 200f), new Vec2(-50f, 0f), new Vec2(500f, 300f) })
            {
                Assert.That(swept.DistanceToCentreLine(point, pose), Is.EqualTo(blade.DistanceToCentreLine(point, pose)).Within(Tolerance));
            }
        }

        [Test]
        public void TheSweptShape_OfTheShield_RunsAcrossItsFace()
        {
            BladeShape face = BladeShape.Of(_shield);
            var facingRight = new BodyPose(new Vec2(100f, 200f), 0f);

            Assert.That(face.HalfWidthUnits, Is.EqualTo(20f));
            Assert.That(face.DistanceToCentreLine(new Vec2(100f, 250f), facingRight), Is.EqualTo(0f).Within(Tolerance), "on the face");
            Assert.That(face.DistanceToCentreLine(new Vec2(130f, 200f), facingRight), Is.EqualTo(30f).Within(Tolerance), "in front of it");
            Assert.That(face.DistanceToCentreLine(new Vec2(100f, 300f), facingRight), Is.EqualTo(25f).Within(Tolerance), "past its top");
        }
    }
}
