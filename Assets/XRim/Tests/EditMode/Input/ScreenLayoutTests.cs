using System;
using NUnit.Framework;
using XRim.Core;
using XRim.Input;
using XRim.Rules;

namespace XRim.Tests.EditMode.Input
{
    /// <summary>GDD §4: the body zone is the strip of <c>BodyZoneWidthFraction</c> over the player's own dummy, the rest is the weapon zone.</summary>
    public sealed class ScreenLayoutTests
    {
        private const float BodyZoneFraction = 0.15f;

        private static ScreenLayout Layout(float width, float height, bool mirrored = false, Side ownSide = Side.Left) =>
            new ScreenLayout(width, height, BodyZoneFraction, mirrored, ownSide);

        [TestCase(1280f, 720f)]
        [TestCase(1920f, 1080f)]
        [TestCase(2400f, 1080f)]
        public void TheSplitFollowsTheShareOfTheWidth_AtAnyResolution(float width, float height)
        {
            ScreenLayout layout = Layout(width, height);
            Assert.That(layout.ZoneAt(new Vec2(width * 0.10f, height * 0.5f)), Is.EqualTo(ScreenZone.Body));
            Assert.That(layout.ZoneAt(new Vec2(width * 0.14f, height * 0.5f)), Is.EqualTo(ScreenZone.Body));
            Assert.That(layout.ZoneAt(new Vec2(width * 0.16f, height * 0.5f)), Is.EqualTo(ScreenZone.Weapon));
            Assert.That(layout.ZoneAt(new Vec2(width * 0.90f, height * 0.5f)), Is.EqualTo(ScreenZone.Weapon));
        }

        [TestCase(1280f, 720f)]
        [TestCase(1920f, 1080f)]
        [TestCase(2400f, 1080f)]
        public void Mirrored_TheBodyZoneIsTheStripAtTheRightEdge(float width, float height)
        {
            ScreenLayout layout = Layout(width, height, mirrored: true);
            Assert.That(layout.ZoneAt(new Vec2(width * 0.90f, height * 0.5f)), Is.EqualTo(ScreenZone.Body));
            Assert.That(layout.ZoneAt(new Vec2(width * 0.86f, height * 0.5f)), Is.EqualTo(ScreenZone.Body));
            Assert.That(layout.ZoneAt(new Vec2(width * 0.84f, height * 0.5f)), Is.EqualTo(ScreenZone.Weapon));
            Assert.That(layout.ZoneAt(new Vec2(width * 0.10f, height * 0.5f)), Is.EqualTo(ScreenZone.Weapon));
        }

        [Test]
        public void TheEdgeOfTheStripBelongsToTheBodyZone_AndHeightNeverMatters()
        {
            ScreenLayout layout = Layout(1000f, 500f);
            Assert.That(layout.ZoneAt(new Vec2(150f, 0f)), Is.EqualTo(ScreenZone.Body));
            Assert.That(layout.ZoneAt(new Vec2(150f, 500f)), Is.EqualTo(ScreenZone.Body));
            Assert.That(layout.ZoneAt(new Vec2(150.5f, 250f)), Is.EqualTo(ScreenZone.Weapon));
        }

        [TestCase(Side.Left)]
        [TestCase(Side.Right)]
        public void NotMirrored_TheOwnDummyIsAlwaysOnTheLeftOfTheScreen_WhicheverArenaSideItPlays(Side ownSide)
        {
            // A right-side player's arena is flipped so their dummy appears on the left; the zones do not care.
            ScreenLayout layout = Layout(1000f, 500f, false, ownSide);
            Assert.That(layout.ZoneAt(new Vec2(50f, 250f)), Is.EqualTo(ScreenZone.Body));
            Assert.That(layout.ZoneAt(new Vec2(950f, 250f)), Is.EqualTo(ScreenZone.Weapon));
            Assert.That(layout.ForwardScreenSign, Is.EqualTo(1f));
        }

        [TestCase(Side.Left)]
        [TestCase(Side.Right)]
        public void Mirrored_ForwardPointsToTheLeftOfTheScreen(Side ownSide)
        {
            Assert.That(Layout(1000f, 500f, true, ownSide).ForwardScreenSign, Is.EqualTo(-1f));
        }

        // Sideways input means the same move for swipes and keys: toward the opponent as the player sees it is a lunge.
        [TestCase(false, Side.Left, BodyMove.Lunge)]
        [TestCase(false, Side.Right, BodyMove.Lunge)]
        [TestCase(true, Side.Left, BodyMove.StepBack)]
        [TestCase(true, Side.Right, BodyMove.StepBack)]
        public void ScreenRight_IsForwardUnlessMirrored_WhicheverArenaSideIsPlayed(bool mirrored, Side ownSide, BodyMove rightMeans)
        {
            ScreenLayout layout = Layout(1000f, 500f, mirrored, ownSide);
            BodyMove leftMeans = rightMeans == BodyMove.Lunge ? BodyMove.StepBack : BodyMove.Lunge;

            Assert.That(layout.HorizontalMove(1f), Is.EqualTo(rightMeans));
            Assert.That(layout.HorizontalMove(-1f), Is.EqualTo(leftMeans));
            Assert.That(layout.HorizontalMove(0f), Is.Null);
        }

        [Test]
        public void WithScreenSize_KeepsEverythingElse()
        {
            ScreenLayout resized = Layout(1000f, 500f, true, Side.Right).WithScreenSize(2400f, 1080f);

            Assert.That(resized.ScreenWidthPixels, Is.EqualTo(2400f));
            Assert.That(resized.ScreenHeightPixels, Is.EqualTo(1080f));
            Assert.That(resized.IsMirrored, Is.True);
            Assert.That(resized.OwnSide, Is.EqualTo(Side.Right));
            Assert.That(resized.BodyZoneWidthFraction, Is.EqualTo(BodyZoneFraction));
        }

        [Test]
        public void ADummyStandingFartherIn_WidensTheBodyZoneToCoverIt()
        {
            ScreenLayout layout = Layout(2000f, 900f).WithDummyCover(0.32f);

            Assert.That(layout.EffectiveBodyZoneFraction, Is.EqualTo(0.32f));
            Assert.That(layout.BodyZoneWidthFraction, Is.EqualTo(BodyZoneFraction), "the configured width stays the least");
            Assert.That(layout.ZoneAt(new Vec2(2000f * 0.30f, 450f)), Is.EqualTo(ScreenZone.Body));
            Assert.That(layout.ZoneAt(new Vec2(2000f * 0.34f, 450f)), Is.EqualTo(ScreenZone.Weapon));
        }

        [Test]
        public void ADummyInsideTheStrip_LeavesTheStripAsItIs()
        {
            ScreenLayout layout = Layout(2000f, 900f).WithDummyCover(0.1f);

            Assert.That(layout.EffectiveBodyZoneFraction, Is.EqualTo(BodyZoneFraction));
        }

        [Test]
        public void Mirrored_TheCoveredZoneGrowsFromTheRightEdge()
        {
            ScreenLayout layout = Layout(2000f, 900f, mirrored: true).WithDummyCover(0.3f);

            Assert.That(layout.ZoneAt(new Vec2(2000f * 0.72f, 450f)), Is.EqualTo(ScreenZone.Body));
            Assert.That(layout.ZoneAt(new Vec2(2000f * 0.68f, 450f)), Is.EqualTo(ScreenZone.Weapon));
        }

        [Test]
        public void WithScreenSize_KeepsTheCover()
        {
            ScreenLayout resized = Layout(1000f, 500f).WithDummyCover(0.3f).WithScreenSize(2400f, 1080f);

            Assert.That(resized.DummyCoverFraction, Is.EqualTo(0.3f));
        }

        [Test]
        public void AScreenWithNoSizeIsRefused()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Layout(0f, 500f));
            Assert.Throws<ArgumentOutOfRangeException>(() => Layout(1000f, 0f));
        }
    }
}
