using NUnit.Framework;
using XRim.Core;
using XRim.Presentation.Cameras;
using XRim.Rules;
using XRim.Rules.Arena;
using XRim.Rules.Combat;
using XRim.Rules.Damage;
using XRim.Rules.Events;

namespace XRim.Tests.EditMode.Presentation
{
    /// <summary>
    /// The camera's reactions (Session 13, GDD §2 camera TBD, flagged): which events shake it and how hard, which ones punch in, the shake
    /// fading out the same way every time, and the punch-in easing in, holding and easing back.
    /// </summary>
    public sealed class CameraReactionTests
    {
        private const float Tolerance = 1e-4f;
        private static readonly SimTime Time = SimTime.FromSeconds(0.25);

        private static CameraFeelSettings Settings() => new CameraFeelSettings
        {
            ShakeOnImpacts = true, ShakeBaseViewFraction = 0.003f, ShakePerDamageViewFraction = 0.001f, MaxShakeViewFraction = 0.025f,
            CrushShakeViewFraction = 0.016f, ClashShakeViewFraction = 0.008f, WallZapShakeViewFraction = 0.018f, ShakeSeconds = 0.25f,
            ShakeFrequencyHz = 22f, PunchInOnBigMoments = true, PunchInViewFraction = 0.86f, BigPunchInViewFraction = 0.78f,
            PunchInSeconds = 0.06f, PunchHoldSeconds = 0.18f, PunchOutSeconds = 0.35f,
        };

        private static HitLandedEvent Hit(float damage) => new HitLandedEvent(Time, new HitFacts(Side.Left, BodyPart.Torso, WeaponIds.Rapier, Time, false),
            new DamageResult(damage, damage, 0f, false, false));

        // --- What shakes and what punches in ---------------------------------------------------

        [Test]
        public void AHit_ShakesByItsDamage_UpToTheStrongestShake()
        {
            CameraFeelSettings settings = Settings();

            Assert.That(settings.ShakeFor(Hit(8f)), Is.EqualTo(0.011f).Within(Tolerance));
            Assert.That(settings.ShakeFor(Hit(100f)), Is.EqualTo(0.025f).Within(Tolerance));
        }

        [Test]
        public void BigMoments_ShakeTheirOwnAmounts()
        {
            CameraFeelSettings settings = Settings();

            Assert.That(settings.ShakeFor(new FighterDiedEvent(Time, Side.Right)), Is.EqualTo(0.025f));
            Assert.That(settings.ShakeFor(new LimbSeveredEvent(Time, Side.Right, BodyPart.LeftLeg)), Is.EqualTo(0.025f));
            Assert.That(settings.ShakeFor(Clash(ClashKind.CrushThrough, Side.Left)), Is.EqualTo(0.016f));
            Assert.That(settings.ShakeFor(Clash(ClashKind.BothRebound, null)), Is.EqualTo(0.008f));
            Assert.That(settings.ShakeFor(Clash(ClashKind.BothSlidePast, null)), Is.EqualTo(0f));
            Assert.That(settings.ShakeFor(new ElectricWallEvent(Time, Side.Left, ElectricWallPhase.Active, 10f, -400f)), Is.EqualTo(0.018f));
            Assert.That(settings.ShakeFor(new ElectricWallEvent(Time, Side.Left, ElectricWallPhase.Warning, 0f, -400f)), Is.EqualTo(0f));
        }

        [Test]
        public void SeversAndDeaths_PunchInTheMost_CrushesAndCounterHitsALittle_HitsNot()
        {
            CameraFeelSettings settings = Settings();

            Assert.That(settings.PunchFor(new FighterDiedEvent(Time, Side.Right)), Is.EqualTo(0.78f));
            Assert.That(settings.PunchFor(new LimbSeveredEvent(Time, Side.Right, BodyPart.LeftArm)), Is.EqualTo(0.78f));
            Assert.That(settings.PunchFor(Clash(ClashKind.CrushThrough, Side.Left)), Is.EqualTo(0.86f));
            Assert.That(settings.PunchFor(new AttackInterruptedEvent(Time, Side.Right)), Is.EqualTo(0.86f));
            Assert.That(settings.PunchFor(Clash(ClashKind.BothRebound, null)), Is.EqualTo(1f));
            Assert.That(settings.PunchFor(Hit(30f)), Is.EqualTo(1f));
        }

        [Test]
        public void SwitchedOff_NothingShakesOrPunchesIn()
        {
            CameraFeelSettings settings = Settings();
            settings.ShakeOnImpacts = false;
            settings.PunchInOnBigMoments = false;

            Assert.That(settings.ShakeFor(new FighterDiedEvent(Time, Side.Right)), Is.EqualTo(0f));
            Assert.That(settings.PunchFor(new FighterDiedEvent(Time, Side.Right)), Is.EqualTo(1f));
        }

        // --- Shake -------------------------------------------------------------------------------

        [Test]
        public void AShake_FadesOut_AndStopsAfterItsTime()
        {
            var shake = new CameraShake();
            shake.Trigger(0.02f, 0.25f, 22f, 7u);
            Assert.That(shake.IsActive, Is.True);

            float strongest = 0f;
            for (int frame = 0; frame < 16; frame++)
            {
                Vec2 offset = shake.Offset(1f);
                strongest = System.Math.Max(strongest, System.Math.Max(System.Math.Abs(offset.X), System.Math.Abs(offset.Y)));
                shake.Advance(1f / 60f);
            }

            Assert.That(strongest, Is.GreaterThan(0f).And.LessThanOrEqualTo(0.02f));
            Assert.That(shake.IsActive, Is.False, "16 frames of 1/60 s outlast 0.25 s");
            Assert.That(shake.Offset(1f), Is.EqualTo(Vec2.Zero));
        }

        [Test]
        public void TheSameEvent_ShakesTheSameWay()
        {
            var first = new CameraShake();
            var second = new CameraShake();
            first.Trigger(0.02f, 0.25f, 22f, 1234u);
            second.Trigger(0.02f, 0.25f, 22f, 1234u);
            first.Advance(0.03f);
            second.Advance(0.03f);

            Assert.That(second.Offset(1f), Is.EqualTo(first.Offset(1f)));
        }

        [Test]
        public void AShake_StaysWithinTheStrongestAllowed()
        {
            var shake = new CameraShake();
            for (uint seed = 1u; seed <= 5u; seed++) shake.Trigger(0.02f, 0.25f, 22f, seed);

            for (int frame = 0; frame < 10; frame++)
            {
                Vec2 offset = shake.Offset(0.025f);
                Assert.That(System.Math.Abs(offset.X), Is.LessThanOrEqualTo(0.025f));
                Assert.That(System.Math.Abs(offset.Y), Is.LessThanOrEqualTo(0.025f));
                shake.Advance(0.01f);
            }
        }

        // --- Punch-in ---------------------------------------------------------------------------

        [Test]
        public void APunchIn_EasesIn_Holds_ThenEasesBackOut()
        {
            var punch = new CameraPunch();
            punch.Trigger(new Vec2(200f, 150f), 0.8f, 0.1f, 0.2f, 0.3f);

            Assert.That(punch.ZoomFraction, Is.EqualTo(1f));
            punch.Advance(0.05f);
            Assert.That(punch.ZoomFraction, Is.EqualTo(0.9f).Within(Tolerance), "halfway in (smoothstep is symmetric)");
            punch.Advance(0.1f);
            Assert.That(punch.ZoomFraction, Is.EqualTo(0.8f).Within(Tolerance), "holding");
            punch.Advance(0.3f);
            Assert.That(punch.ZoomFraction, Is.EqualTo(0.9f).Within(Tolerance), "halfway out");
            punch.Advance(0.2f);
            Assert.That(punch.IsActive, Is.False);
            Assert.That(punch.ZoomFraction, Is.EqualTo(1f));
            Assert.That(punch.FocusUnits, Is.EqualTo(new Vec2(200f, 150f)));
        }

        [Test]
        public void AWeakerPunch_DoesNotCutAStrongerOneShort_AStrongerOneTakesOver()
        {
            var punch = new CameraPunch();
            punch.Trigger(new Vec2(100f, 100f), 0.78f, 0.06f, 0.18f, 0.35f);
            punch.Advance(0.1f);

            punch.Trigger(new Vec2(300f, 100f), 0.86f, 0.06f, 0.18f, 0.35f);
            Assert.That(punch.FocusUnits, Is.EqualTo(new Vec2(100f, 100f)), "the weaker one is ignored");

            punch.Trigger(new Vec2(500f, 100f), 0.7f, 0.06f, 0.18f, 0.35f);
            Assert.That(punch.FocusUnits, Is.EqualTo(new Vec2(500f, 100f)), "the stronger one takes over");
        }

        private static WeaponClashEvent Clash(ClashKind kind, Side? winner) =>
            new WeaponClashEvent(Time, new ClashResult(kind, winner, 60f, 3f, 12f), new Vec2(300f, 120f));
    }
}
