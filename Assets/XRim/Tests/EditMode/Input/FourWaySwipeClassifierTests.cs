using System;
using NUnit.Framework;
using XRim.Core;
using XRim.Input;
using XRim.Rules;

namespace XRim.Tests.EditMode.Input
{
    /// <summary>GDD §5: down crouches, up jumps, forward lunges, backward steps back. D14: no diagonals. Screen positions are Unity pixels (Y up).</summary>
    public sealed class FourWaySwipeClassifierTests
    {
        private const float WindowSeconds = 0.3f;
        private const float MinDistanceFraction = 0.03f;
        private const float ToleranceDegrees = 30f;
        private const float QuickSeconds = 0.1f;
        private const float Width = 1920f;
        private const float Height = 1080f;
        private const float BodyZoneFraction = 0.15f;
        private static readonly Vec2 Start = new Vec2(100f, 500f);

        private readonly FourWaySwipeClassifier _classifier =
            new FourWaySwipeClassifier(new SwipeSettings(WindowSeconds, MinDistanceFraction, ToleranceDegrees));

        private static ScreenLayout Layout(bool mirrored = false, Side ownSide = Side.Left, float height = Height) =>
            new ScreenLayout(Width, height, BodyZoneFraction, mirrored, ownSide);

        private BodyMove? Swipe(float dx, float dy, ScreenLayout layout = null, float seconds = QuickSeconds) =>
            _classifier.Classify(Start, Start + new Vec2(dx, dy), seconds, layout ?? Layout());

        // Classification table. Forward is toward the opponent: screen-right normally, screen-left when mirrored,
        // whichever arena side the player controls.
        [TestCase(false, Side.Left, 200f, 0f, BodyMove.Lunge)]
        [TestCase(false, Side.Left, -200f, 0f, BodyMove.StepBack)]
        [TestCase(false, Side.Left, 0f, 200f, BodyMove.Jump)]
        [TestCase(false, Side.Left, 0f, -200f, BodyMove.Crouch)]
        [TestCase(false, Side.Right, 200f, 0f, BodyMove.Lunge)]
        [TestCase(false, Side.Right, -200f, 0f, BodyMove.StepBack)]
        [TestCase(false, Side.Right, 0f, 200f, BodyMove.Jump)]
        [TestCase(false, Side.Right, 0f, -200f, BodyMove.Crouch)]
        [TestCase(true, Side.Left, 200f, 0f, BodyMove.StepBack)]
        [TestCase(true, Side.Left, -200f, 0f, BodyMove.Lunge)]
        [TestCase(true, Side.Left, 0f, 200f, BodyMove.Jump)]
        [TestCase(true, Side.Left, 0f, -200f, BodyMove.Crouch)]
        [TestCase(true, Side.Right, 200f, 0f, BodyMove.StepBack)]
        [TestCase(true, Side.Right, -200f, 0f, BodyMove.Lunge)]
        [TestCase(true, Side.Right, 0f, 200f, BodyMove.Jump)]
        [TestCase(true, Side.Right, 0f, -200f, BodyMove.Crouch)]
        public void TheFourDirections(bool mirrored, Side ownSide, float dx, float dy, BodyMove expected)
        {
            Assert.That(Swipe(dx, dy, Layout(mirrored, ownSide)), Is.EqualTo(expected));
        }

        [Test]
        public void ASlightlyCrookedFlickStillCounts()
        {
            Assert.That(Swipe(200f, 50f), Is.EqualTo(BodyMove.Lunge));
            Assert.That(Swipe(-200f, -50f), Is.EqualTo(BodyMove.StepBack));
            Assert.That(Swipe(50f, 200f), Is.EqualTo(BodyMove.Jump));
            Assert.That(Swipe(-50f, -200f), Is.EqualTo(BodyMove.Crouch));
        }

        [Test]
        public void ToleranceEdge_JustInsideCounts_JustOutsideDoesNothing()
        {
            Assert.That(Swipe(200f, 200f * Tan(ToleranceDegrees - 1f)), Is.EqualTo(BodyMove.Lunge));
            Assert.That(Swipe(200f, 200f * Tan(ToleranceDegrees + 1f)), Is.Null);
            Assert.That(Swipe(200f * Tan(ToleranceDegrees - 1f), 200f), Is.EqualTo(BodyMove.Jump));
            Assert.That(Swipe(200f * Tan(ToleranceDegrees + 1f), 200f), Is.Null);
        }

        [TestCase(150f, 150f)]
        [TestCase(-150f, 150f)]
        [TestCase(150f, -150f)]
        [TestCase(-150f, -150f)]
        public void D14_ADiagonalDoesNothing(float dx, float dy)
        {
            Assert.That(Swipe(dx, dy), Is.Null);
        }

        [Test]
        public void ATolerance_Of45_LeavesNoGap()
        {
            var noGap = new FourWaySwipeClassifier(new SwipeSettings(WindowSeconds, MinDistanceFraction, SwipeSettings.MaxAxisToleranceDegrees));

            Assert.That(noGap.Classify(Start, Start + new Vec2(150f, 140f), QuickSeconds, Layout()), Is.EqualTo(BodyMove.Lunge));
            Assert.That(noGap.Classify(Start, Start + new Vec2(140f, 150f), QuickSeconds, Layout()), Is.EqualTo(BodyMove.Jump));
        }

        [Test]
        public void ADragThatIsTooShortDoesNothing_ByShareOfTheScreenHeight()
        {
            float minimum = MinDistanceFraction * Height;
            Assert.That(Swipe(minimum - 2f, 0f), Is.Null);
            Assert.That(Swipe(minimum + 2f, 0f), Is.EqualTo(BodyMove.Lunge));
            Assert.That(_classifier.Classify(Start, Start, QuickSeconds, Layout()), Is.Null, "no movement is no swipe");
        }

        [Test]
        public void TheMinimumDistanceScalesWithTheScreen_SoItFeelsTheSameOnEveryPhone()
        {
            const float pixels = 25f; // 2.3% of a 1080 px screen, 3.5% of a 720 px screen

            Assert.That(Swipe(pixels, 0f, Layout(height: 1080f)), Is.Null);
            Assert.That(Swipe(pixels, 0f, Layout(height: 720f)), Is.EqualTo(BodyMove.Lunge));
        }

        [Test]
        public void AFlickTakesAtMostTheWindow_TheEdgeCounts()
        {
            Assert.That(Swipe(200f, 0f, seconds: 0f), Is.EqualTo(BodyMove.Lunge));
            Assert.That(Swipe(200f, 0f, seconds: WindowSeconds), Is.EqualTo(BodyMove.Lunge));
            Assert.That(Swipe(200f, 0f, seconds: WindowSeconds + 0.01f), Is.Null);
            Assert.That(Swipe(200f, 0f, seconds: -0.1f), Is.Null);
        }

        [Test]
        public void SettingsAreChecked()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SwipeSettings(0f, MinDistanceFraction, ToleranceDegrees));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SwipeSettings(WindowSeconds, 0f, ToleranceDegrees));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SwipeSettings(WindowSeconds, MinDistanceFraction, -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SwipeSettings(WindowSeconds, MinDistanceFraction, SwipeSettings.MaxAxisToleranceDegrees + 1f));
        }

        private static float Tan(float degrees) => (float)Math.Tan(degrees * XMath.DegreesToRadians);
    }
}
