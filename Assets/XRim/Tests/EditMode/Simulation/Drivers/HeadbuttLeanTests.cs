using NUnit.Framework;
using XRim.Core;
using XRim.Simulation.Drivers;
using XRim.Simulation.Physics;

namespace XRim.Tests.EditMode.Simulation.Drivers
{
    /// <summary>
    /// The armless headbutt (GDD §12 default; the lean is a Session 11 feel choice): the body leans in at the pelvis and shifts along the
    /// floor so the head, tilted with the torso, lands on the drawn point. Torso 120 high, head radius 25: standing, the head's centre is
    /// at (0, 145) in the torso frame.
    /// </summary>
    public sealed class HeadbuttLeanTests
    {
        private const float TorsoHeight = 120f;
        private const float Radius = 25f;
        private const float Reach = TorsoHeight + Radius;
        private const float Tolerance = 1e-2f;

        private static void Solve(Vec2 centre, out float lean, out float shift) => HeadbuttLean.Solve(centre, TorsoHeight, Radius, out lean, out shift);

        /// <summary>Where the head's centre ends up for a lean and a shift.</summary>
        private static Vec2 HeadCentre(float lean, float shift) => new Vec2(shift, 0f) + HeadbuttLean.AxisLocal(lean) * Reach;

        [Test]
        public void Standing_TheHeadIsWhereItRests_WithNoLeanOrShift()
        {
            Solve(new Vec2(0f, Reach), out float lean, out float shift);

            Assert.That(lean, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(shift, Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void ALowPointAhead_LeansIn_AndTheHeadLandsOnIt()
        {
            var target = new Vec2(150f, 90f);

            Solve(target, out float lean, out float shift);

            Assert.That(lean, Is.EqualTo(51.6f).Within(0.1f), "acos(90 / 145)");
            Assert.That(shift, Is.GreaterThan(0f), "a little forward along the floor");
            Assert.That(HeadCentre(lean, shift).X, Is.EqualTo(target.X).Within(Tolerance));
            Assert.That(HeadCentre(lean, shift).Y, Is.EqualTo(target.Y).Within(Tolerance));
        }

        [Test]
        public void APointAtHeadHeight_IsReachedStandingUp_ByShiftingAlone()
        {
            Solve(new Vec2(150f, Reach), out float lean, out float shift);

            Assert.That(lean, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(shift, Is.EqualTo(150f).Within(Tolerance));
        }

        [Test]
        public void APointBehind_LeansBack()
        {
            Solve(new Vec2(-60f, 100f), out float lean, out _);

            Assert.That(lean, Is.LessThan(0f));
        }

        [Test]
        public void TooHighOrBelowThePelvis_IsReachedAtTheNearestHeightItCan()
        {
            Solve(new Vec2(40f, 400f), out float upLean, out float upShift);
            Solve(new Vec2(100f, -80f), out float downLean, out float downShift);

            Assert.That(upLean, Is.EqualTo(0f).Within(Tolerance), "it cannot rise above standing");
            Assert.That(HeadCentre(upLean, upShift).Y, Is.EqualTo(Reach).Within(Tolerance));
            Assert.That(downLean, Is.EqualTo(90f).Within(Tolerance), "never past horizontal");
            Assert.That(HeadCentre(downLean, downShift).X, Is.EqualTo(100f).Within(Tolerance));
            Assert.That(HeadCentre(downLean, downShift).Y, Is.EqualTo(0f).Within(Tolerance), "at the pelvis's height");
        }

        [Test]
        public void TheAimModel_TiltsTheHeadWithTheLean_SoItsCentreIsTheDrawnPoint()
        {
            var aim = new HeadStrikeAimModel(Radius, TorsoHeight);
            var target = new Vec2(150f, 90f);
            Solve(target, out float lean, out _);

            BodyPose item = aim.Aim(target, new Vec2(0f, 100f), 240f, 160f);

            Vec2 centre = item.PositionUnits + Vec2.FromAngleDegrees(item.RotationDegrees) * Radius;
            Assert.That(centre.X, Is.EqualTo(target.X).Within(Tolerance));
            Assert.That(centre.Y, Is.EqualTo(target.Y).Within(Tolerance));
            Assert.That(item.RotationDegrees, Is.EqualTo(HeldItemShape.HeadItemRotationOffsetDegrees - lean).Within(Tolerance),
                "tilted toward the opponent with the torso");
            Assert.That(aim.Aim(new Vec2(0f, Reach), Vec2.Zero, 0f, 0f), Is.EqualTo(new BodyPose(new Vec2(0f, TorsoHeight), 90f)),
                "at rest: upright on the neck");
        }
    }
}
