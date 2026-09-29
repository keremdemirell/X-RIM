using NUnit.Framework;
using XRim.Core;
using XRim.Input;
using XRim.Rules;

namespace XRim.Tests.EditMode.Input
{
    public sealed class InputSanityTests
    {
        private const float ScreenWidth = 1000f;
        private const float BodyZoneFraction = 0.15f;
        private const float MidHeight = 500f;

        [Test]
        public void RightHanded_BodyZoneIsOnTheLeftEdge()
        {
            var layout = new ScreenLayout(ScreenWidth, BodyZoneFraction, false, Side.Left);
            Assert.That(layout.ZoneAt(new Vec2(100f, MidHeight)), Is.EqualTo(ScreenZone.Body));
            Assert.That(layout.ZoneAt(new Vec2(500f, MidHeight)), Is.EqualTo(ScreenZone.Weapon));
            Assert.That(layout.ZoneAt(new Vec2(900f, MidHeight)), Is.EqualTo(ScreenZone.Weapon));
        }

        [Test]
        public void LeftHanded_LayoutIsMirrored()
        {
            var layout = new ScreenLayout(ScreenWidth, BodyZoneFraction, true, Side.Left);
            Assert.That(layout.ZoneAt(new Vec2(900f, MidHeight)), Is.EqualTo(ScreenZone.Body));
            Assert.That(layout.ZoneAt(new Vec2(100f, MidHeight)), Is.EqualTo(ScreenZone.Weapon));
            Assert.That(new MirrorWhenLeftHanded().IsScreenMirrored(Handedness.Left), Is.True);
            Assert.That(new MirrorWhenLeftHanded().IsScreenMirrored(Handedness.Right), Is.False);
        }

        [Test]
        public void ArenaView_FlipsSoOwnDummyIsOnThePlayersSide()
        {
            Assert.That(new ScreenLayout(ScreenWidth, BodyZoneFraction, false, Side.Left).ArenaViewFlipped, Is.False);
            Assert.That(new ScreenLayout(ScreenWidth, BodyZoneFraction, false, Side.Right).ArenaViewFlipped, Is.True);
            Assert.That(new ScreenLayout(ScreenWidth, BodyZoneFraction, true, Side.Left).ArenaViewFlipped, Is.True);
            Assert.That(new ScreenLayout(ScreenWidth, BodyZoneFraction, true, Side.Right).ArenaViewFlipped, Is.False);
        }
    }
}
