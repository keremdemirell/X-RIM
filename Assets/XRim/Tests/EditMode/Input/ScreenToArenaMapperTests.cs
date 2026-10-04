using System;
using System.Collections.Generic;
using NUnit.Framework;
using XRim.Core;
using XRim.Input;
using XRim.Rules;
using XRim.Rules.Paths;
using XRim.Rules.Settings;
using XRim.Simulation.Drivers;
using XRim.Simulation.Physics;
using XRim.Tests.EditMode.Rules.Paths;

namespace XRim.Tests.EditMode.Input
{
    /// <summary>
    /// GDD §4 input fairness (Decided): pixels become arena units by what the screen shows, so resolution, aspect ratio and
    /// touch rate never change a budget or an outcome.
    /// </summary>
    public sealed class ScreenToArenaMapperTests
    {
        private const float BodyZoneFraction = 0.15f;
        private const float Tolerance = 0.01f;

        /// <summary>The shape is sampled almost unrounded here; each screen then rounds it to its own whole pixels.</summary>
        private const float FingerPrecisionPerUnit = 1000f;

        // What the sandbox camera shows: orthographic size 5.75 at height 5.25, 0.01 world units per arena unit.
        private static readonly ArenaViewFrame View = new ArenaViewFrame(new Vec2(0f, 525f), 1150f);

        private readonly ScreenToArenaMapper _mapper = new ScreenToArenaMapper();

        private static ScreenLayout Layout(float width, float height, bool mirrored = false, Side ownSide = Side.Left) =>
            new ScreenLayout(width, height, BodyZoneFraction, mirrored, ownSide);

        private static void AssertAt(Vec2 actual, float x, float y) =>
            Assert.That(Vec2.Distance(actual, new Vec2(x, y)), Is.LessThan(Tolerance), $"expected ({x}, {y}) but was {actual}");

        [TestCase(1280f, 720f)]
        [TestCase(1920f, 1080f)]
        [TestCase(2400f, 1080f)]
        public void TheScreenCentreIsTheCentreOfTheView(float width, float height)
        {
            AssertAt(_mapper.ToArena(new Vec2(width * 0.5f, height * 0.5f), Layout(width, height), View), 0f, 525f);
        }

        [TestCase(1280f, 720f)]
        [TestCase(1920f, 1080f)]
        [TestCase(2400f, 1080f)]
        public void TheWholeScreenHeightIsTheVisibleHeight_AndThePixelsAreSquare(float width, float height)
        {
            ScreenLayout layout = Layout(width, height);

            AssertAt(_mapper.ToArena(new Vec2(width * 0.5f, height), layout, View), 0f, 1100f);
            AssertAt(_mapper.ToArena(new Vec2(width * 0.5f, 0f), layout, View), 0f, -50f);
            // Half a screen height to the right is 575 units, however wide the screen is.
            AssertAt(_mapper.ToArena(new Vec2(width * 0.5f + height * 0.5f, height * 0.5f), layout, View), 575f, 525f);
        }

        [Test]
        public void AFlippedViewShowsTheArenaMirrored_SoTheMapperMirrorsItBack()
        {
            ScreenLayout flipped = Layout(2000f, 1000f, false, Side.Right);
            Assert.That(flipped.ArenaViewFlipped, Is.True);

            // 250 px right of the centre is 287.5 units: +287.5 normally, -287.5 when the view is mirrored.
            var pixel = new Vec2(1000f + 250f, 500f);
            AssertAt(_mapper.ToArena(pixel, Layout(2000f, 1000f), View), 287.5f, 525f);
            AssertAt(_mapper.ToArena(pixel, flipped, View), -287.5f, 525f);
        }

        [Test]
        public void ToFighterFrame_LeftFighter_ForwardIsScreenRight()
        {
            var root = new BodyPose(new Vec2(-350f, 120f), 0f);
            ScreenLayout layout = Layout(2000f, 1000f);

            // 100 px right of the centre is 115 units; the fighter stands 350 left of it and 405 below the centre height.
            Vec2 local = _mapper.ToFighterFrame(new Vec2(1100f, 500f), layout, View, root, Side.Left);

            AssertAt(local, 115f + 350f, 525f - 120f);
        }

        [Test]
        public void ToFighterFrame_RightFighter_ForwardIsStillTowardTheOpponent_EvenThoughTheArenaIsMirroredBack()
        {
            // A right-side player (not left-handed): the view is flipped so their dummy, at arena x = +350, is drawn left of
            // the centre. A touch 100 px right of the centre is toward the opponent, so it has to read as +X in the frame.
            var root = new BodyPose(new Vec2(350f, 120f), 0f);
            ScreenLayout layout = Layout(2000f, 1000f, false, Side.Right);

            Vec2 local = _mapper.ToFighterFrame(new Vec2(1100f, 500f), layout, View, root, Side.Right);

            // Shown x = +115, so the arena x = -115, which is 465 toward the opponent from the fighter at +350.
            AssertAt(local, 350f + 115f, 525f - 120f);
        }

        [Test]
        public void ToFighterFrame_MirroredLayout_LeftFighterOnTheRight_ForwardIsScreenLeft()
        {
            // A left-handed player on the left side: the view is flipped and their dummy (arena x = -350) is drawn right of the centre.
            var root = new BodyPose(new Vec2(-350f, 120f), 0f);
            ScreenLayout layout = Layout(2000f, 1000f, true, Side.Left);
            Assert.That(layout.ArenaViewFlipped, Is.True);

            Vec2 local = _mapper.ToFighterFrame(new Vec2(900f, 500f), layout, View, root, Side.Left);

            // 100 px left of the centre is shown at x = -115, which is the arena x = +115: 465 forward of the fighter.
            AssertAt(local, 115f + 350f, 525f - 120f);
        }

        [Test]
        public void ToFighterFrame_FollowsTheRootsRotation()
        {
            var root = new BodyPose(new Vec2(0f, 525f), 90f);

            // 100 px above the centre is 115 units straight up in the arena; with the frame turned 90° that is +X in the frame.
            Vec2 local = _mapper.ToFighterFrame(new Vec2(1000f, 600f), Layout(2000f, 1000f), View, root, Side.Left);

            AssertAt(local, 115f, 0f);
        }

        [Test]
        public void ARealViewHasToHaveASize()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ArenaViewFrame(Vec2.Zero, 0f));
        }

        [TestCase(1280f, 720f, false, Side.Left)]
        [TestCase(1920f, 1080f, false, Side.Left)]
        [TestCase(2400f, 1080f, false, Side.Left)]
        [TestCase(1280f, 720f, true, Side.Left)]
        [TestCase(2400f, 1080f, true, Side.Right)]
        public void TheSameDrawnShapeCostsTheSameInk_AtAnyResolution(float width, float height, bool mirrored, Side ownSide)
        {
            float referenceInk = InkOf(Drawn(1920f, 1080f, false, Side.Left));

            float ink = InkOf(Drawn(width, height, mirrored, ownSide));

            // Whole pixels at 720p are 1.6 units wide, so the ink may differ by a hair.
            Assert.That(ink, Is.EqualTo(referenceInk).Within(referenceInk * 0.01f), $"{width}x{height} mirrored={mirrored} side={ownSide}");
        }

        /// <summary>
        /// The torso-frame shape every resolution draws: a 300-unit weaving thrust at shoulder height, as a finger on the
        /// screen would make it for the fighter's dummy: sampled at 60 Hz with uneven speed, positions rounded to whole
        /// pixels of this screen. Short enough to stay inside the rapier's ink, so the comparison is not about where it is cut.
        /// </summary>
        private WeaponPath Drawn(float width, float height, bool mirrored, Side ownSide)
        {
            ScreenLayout layout = Layout(width, height, mirrored, ownSide);
            Side side = ownSide;
            var root = new BodyPose(new Vec2(side == Side.Left ? -350f : 350f, 120f), 0f);
            var shoulderOffset = new Vec2(100f, 100f);
            float pixelsPerUnit = height / View.VisibleHeightUnits;

            List<Vec2> torsoShape = FingerStroke.Sample(
                t => shoulderOffset + new Vec2(300f * t, 40f * (float)Math.Sin(2.0 * Math.PI * t)), 0.5f, 60f, FingerPrecisionPerUnit);
            var local = new List<Vec2>();
            foreach (Vec2 point in torsoShape)
            {
                Vec2 arena = TorsoFrame.ToArena(point, root, side);
                float shownX = layout.ArenaViewFlipped ? -arena.X : arena.X;
                var pixel = new Vec2(
                    (float)Math.Round(width * 0.5f + (shownX - View.CenterUnits.X) * pixelsPerUnit),
                    (float)Math.Round(height * 0.5f + (arena.Y - View.CenterUnits.Y) * pixelsPerUnit));
                local.Add(_mapper.ToFighterFrame(pixel, layout, View, root, side));
            }

            return new WeaponPath(local);
        }

        private static float InkOf(WeaponPath stroke)
        {
            BuiltPath built = new PathBuilder(new RulePolicies()).Build(WeaponPath.Empty, stroke, new Vec2(0f, 100f),
                GddStartingValues.Rapier(), new PathSettings());
            return built.Inked.Ink.CostUnits;
        }
    }
}
