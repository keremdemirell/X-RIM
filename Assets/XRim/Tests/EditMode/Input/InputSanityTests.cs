using NUnit.Framework;
using XRim.Core;
using XRim.Input;
using XRim.Rules;

namespace XRim.Tests.EditMode.Input
{
    public sealed class InputSanityTests
    {
        private const float ScreenWidth = 1000f;
        private const float ScreenHeight = 500f;
        private const float BodyZoneFraction = 0.15f;
        private const float MidHeight = 250f;

        private static ScreenLayout Layout(bool mirrored, Side ownSide) =>
            new ScreenLayout(ScreenWidth, ScreenHeight, BodyZoneFraction, mirrored, ownSide);

        [Test]
        public void RightHanded_BodyZoneIsOnTheLeftEdge()
        {
            ScreenLayout layout = Layout(false, Side.Left);
            Assert.That(layout.ZoneAt(new Vec2(100f, MidHeight)), Is.EqualTo(ScreenZone.Body));
            Assert.That(layout.ZoneAt(new Vec2(500f, MidHeight)), Is.EqualTo(ScreenZone.Weapon));
            Assert.That(layout.ZoneAt(new Vec2(900f, MidHeight)), Is.EqualTo(ScreenZone.Weapon));
        }

        [Test]
        public void LeftHanded_LayoutIsMirrored()
        {
            ScreenLayout layout = Layout(true, Side.Left);
            Assert.That(layout.ZoneAt(new Vec2(900f, MidHeight)), Is.EqualTo(ScreenZone.Body));
            Assert.That(layout.ZoneAt(new Vec2(100f, MidHeight)), Is.EqualTo(ScreenZone.Weapon));
            Assert.That(new MirrorWhenLeftHanded().IsScreenMirrored(Handedness.Left), Is.True);
            Assert.That(new MirrorWhenLeftHanded().IsScreenMirrored(Handedness.Right), Is.False);
        }

        [Test]
        public void ArenaView_FlipsSoOwnDummyIsOnThePlayersSide()
        {
            Assert.That(Layout(false, Side.Left).ArenaViewFlipped, Is.False);
            Assert.That(Layout(false, Side.Right).ArenaViewFlipped, Is.True);
            Assert.That(Layout(true, Side.Left).ArenaViewFlipped, Is.True);
            Assert.That(Layout(true, Side.Right).ArenaViewFlipped, Is.False);
        }
    }
}
