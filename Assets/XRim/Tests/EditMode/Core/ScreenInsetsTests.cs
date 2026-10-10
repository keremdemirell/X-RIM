using NUnit.Framework;
using XRim.Core;

namespace XRim.Tests.EditMode.Core
{
    /// <summary>The space the HUD keeps clear of notches and rounded corners (GDD §4), from Unity's Screen.safeArea.</summary>
    public sealed class ScreenInsetsTests
    {
        private const float Tolerance = 1e-4f;
        private const float ScreenWidth = 2400f;
        private const float ScreenHeight = 1080f;

        [Test]
        public void ASafeAreaThatIsTheWholeScreen_HasNoInsets()
        {
            ScreenInsets insets = ScreenInsets.FromSafeArea(0f, 0f, ScreenWidth, ScreenHeight, ScreenWidth, ScreenHeight);

            Assert.That(insets.IsNone, Is.True);
            Assert.That(insets, Is.EqualTo(ScreenInsets.None));
        }

        [Test]
        public void ANotchOnTheLeftOfALandscapeScreen_InsetsTheLeftEdgeOnly()
        {
            // Unity gives the safe area from the bottom-left: x 80 means 80 pixels are covered on the left.
            ScreenInsets insets = ScreenInsets.FromSafeArea(80f, 0f, ScreenWidth - 80f, ScreenHeight, ScreenWidth, ScreenHeight);

            Assert.That(insets.Left, Is.EqualTo(80f).Within(Tolerance));
            Assert.That(insets.Right, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(insets.Top, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(insets.Bottom, Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void EveryEdgeIsMeasuredFromItsOwnSideOfTheScreen()
        {
            // 100 px left, 60 px right, 20 px at the bottom (a gesture bar), 30 px at the top.
            ScreenInsets insets = ScreenInsets.FromSafeArea(100f, 20f, ScreenWidth - 160f, ScreenHeight - 50f, ScreenWidth, ScreenHeight);

            Assert.That(insets.Left, Is.EqualTo(100f).Within(Tolerance));
            Assert.That(insets.Right, Is.EqualTo(60f).Within(Tolerance));
            Assert.That(insets.Bottom, Is.EqualTo(20f).Within(Tolerance));
            Assert.That(insets.Top, Is.EqualTo(30f).Within(Tolerance));
        }

        [TestCase(0f, 0f, 0f, 2400f, 1080f, TestName = "an empty safe area (a screen not set up yet)")]
        [TestCase(0f, 2400f, 1080f, 0f, 0f, TestName = "no screen size yet")]
        [TestCase(0f, 5000f, 3000f, 2400f, 1080f, TestName = "a safe area bigger than the screen")]
        [TestCase(2500f, 100f, 1080f, 2400f, 1080f, TestName = "a safe area off the side of the screen")]
        public void AnImpossibleSafeArea_MeansNoInsets_NeverAHudSqueezedToNothing(float safeX, float safeW, float safeH, float screenW, float screenH)
        {
            ScreenInsets insets = ScreenInsets.FromSafeArea(safeX, 0f, safeW, safeH, screenW, screenH);

            Assert.That(insets.IsNone, Is.True);
        }

        [Test]
        public void ANegativeInset_IsClampedToZero()
        {
            var insets = new ScreenInsets(-5f, 10f, -1f, 0f);

            Assert.That(insets.Left, Is.EqualTo(0f));
            Assert.That(insets.Right, Is.EqualTo(0f));
            Assert.That(insets.Top, Is.EqualTo(10f));
        }

        [Test]
        public void Scaled_MultipliesEveryInset_ForAUiThatScalesWithTheScreen()
        {
            // A 1080-pixel-high screen shown on a 1920×1080 reference UI is 1 unit per pixel; at 720 pixels it is 1.5 units per pixel.
            ScreenInsets pixels = new ScreenInsets(40f, 0f, 20f, 10f);

            ScreenInsets units = pixels.Scaled(1080f / 720f);

            Assert.That(units.Left, Is.EqualTo(60f).Within(Tolerance));
            Assert.That(units.Right, Is.EqualTo(30f).Within(Tolerance));
            Assert.That(units.Bottom, Is.EqualTo(15f).Within(Tolerance));
        }

        [Test]
        public void Equality_ComparesEveryInset()
        {
            Assert.That(new ScreenInsets(1f, 2f, 3f, 4f), Is.EqualTo(new ScreenInsets(1f, 2f, 3f, 4f)));
            Assert.That(new ScreenInsets(1f, 2f, 3f, 4f), Is.Not.EqualTo(new ScreenInsets(1f, 2f, 3f, 5f)));
        }
    }
}
