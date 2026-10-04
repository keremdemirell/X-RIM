using System;
using System.Collections.Generic;
using NUnit.Framework;
using XRim.Core;
using XRim.Input;
using XRim.Rules;
using XRim.Rules.Paths;
using XRim.Rules.Planning;
using XRim.Rules.Settings;
using static XRim.Tests.EditMode.Input.SchemeRig;

namespace XRim.Tests.EditMode.Input
{
    /// <summary>
    /// GDD §4–5: the body zone's flicks set the body move, drawing in the weapon zone sets the path, and nothing but commands
    /// comes out. Fingers are scripted; positions are Unity screen pixels (Y up).
    /// </summary>
    public sealed class DualZoneInputSchemeTests
    {
        private const float PointTolerance = 0.5f;

        // A finger in the body zone (the left 15% of the screen), and one in the weapon zone, as plain screen spots.
        private static readonly Vec2 BodySpot = new Vec2(100f, 540f);
        private static readonly Vec2 WeaponSpot = new Vec2(900f, 400f);

        private static Vec2 Near(Vec2 p, float dx, float dy) => new Vec2(p.X + dx, p.Y + dy);

        private static Func<float, Vec2> WeavingThrust => t => new Vec2(100f + 300f * t, 100f + 40f * (float)Math.Sin(2.0 * Math.PI * t));

        // ----- drawing -----

        [Test]
        public void AStrokeIsSentWhenTheFingerLifts_NotBefore()
        {
            var rig = new SchemeRig();
            Vec2 start = rig.PixelOf(new Vec2(100f, 100f));
            Vec2 middle = rig.PixelOf(new Vec2(250f, 130f));
            Vec2 end = rig.PixelOf(new Vec2(400f, 100f));

            rig.Tick(Down(1, start, 0.0));
            rig.Tick(Move(1, middle, 0.1));
            Assert.That(rig.Sink.Commands, Is.Empty, "still drawing");
            Assert.That(rig.Scheme.ActiveStroke.Count, Is.EqualTo(2));

            rig.Tick(Move(1, end, 0.2));
            rig.Tick(Up(1, end, 0.25));

            WeaponPath sent = rig.Sink.Only<SetPathCommand>().Path;
            Assert.That(Vec2.Distance(sent.Points[0], new Vec2(100f, 100f)), Is.LessThan(PointTolerance));
            Assert.That(Vec2.Distance(sent.Points[sent.Points.Count - 1], new Vec2(400f, 100f)), Is.LessThan(PointTolerance));
            Assert.That(rig.Scheme.ActiveStroke, Is.Empty, "the stroke is done");
        }

        [Test]
        public void ATap_IsAOnePointStroke_WhereTheFingerLanded_EvenWithJitter()
        {
            var rig = new SchemeRig();

            rig.Tick(Down(1, WeaponSpot, 0.0));
            rig.Tick(Move(1, Near(WeaponSpot, 3f, -2f), 0.05));
            rig.Tick(Up(1, Near(WeaponSpot, 4f, 1f), 0.1));

            WeaponPath sent = rig.Sink.Only<SetPathCommand>().Path;
            Assert.That(sent.Points.Count, Is.EqualTo(1));
            Assert.That(Vec2.Distance(sent.Points[0], rig.LocalOf(WeaponSpot)), Is.LessThan(PointTolerance));
        }

        [Test]
        public void ATapHeldForALongTime_IsStillATap()
        {
            var rig = new SchemeRig();

            rig.Tick(Down(1, WeaponSpot, 0.0));
            rig.Tick(Up(1, WeaponSpot, 3.0));

            Assert.That(rig.Sink.Only<SetPathCommand>().Path.Points.Count, Is.EqualTo(1));
        }

        [TestCase(60f, 60f)]
        [TestCase(120f, 60f)]
        [TestCase(120f, 120f)]
        [TestCase(240f, 30f)]
        public void TheSameShapeGivesTheSamePath_AtAnyTouchRateAndFrameRate(float sampleHz, float frameHz)
        {
            WeaponPath reference = DrawnAndBuilt(60f, 60f, out float referenceInk);

            WeaponPath path = DrawnAndBuilt(sampleHz, frameHz, out float ink);

            Assert.That(ink, Is.EqualTo(referenceInk).Within(referenceInk * 0.01f), $"{sampleHz} Hz touches at {frameHz} fps");
            Assert.That(path.LengthUnits, Is.EqualTo(reference.LengthUnits).Within(reference.LengthUnits * 0.01f));
            Assert.That(Vec2.Distance(Last(path), Last(reference)), Is.LessThan(3f));
        }

        private static Vec2 Last(WeaponPath path) => path.Points[path.Points.Count - 1];

        /// <summary>Draws the weaving thrust at the given rates and returns what the rules make of it.</summary>
        private static WeaponPath DrawnAndBuilt(float sampleHz, float frameHz, out float ink)
        {
            var rig = new SchemeRig();
            rig.Play(rig.Stroke(1, WeavingThrust, 0.5f, sampleHz), frameHz);

            WeaponPath stroke = rig.Sink.Only<SetPathCommand>().Path;
            BuiltPath built = new PathBuilder(new RulePolicies()).Build(WeaponPath.Empty, stroke, new Vec2(0f, 100f),
                GddStartingValues.Rapier(), new PathSettings());
            ink = built.Inked.Ink.CostUnits;
            return built.Path;
        }

        [Test]
        public void AStrokeThatDriftsIntoTheBodyZone_KeepsDrawing()
        {
            var rig = new SchemeRig();
            Vec2 inside = new Vec2(400f, 400f);
            Vec2 overTheBodyZone = new Vec2(150f, 400f);

            rig.Tick(Down(1, inside, 0.0));
            rig.Tick(Move(1, overTheBodyZone, 0.1));
            rig.Tick(Up(1, overTheBodyZone, 0.2));

            WeaponPath sent = rig.Sink.Only<SetPathCommand>().Path;
            Assert.That(Vec2.Distance(Last(sent), rig.LocalOf(overTheBodyZone)), Is.LessThan(PointTolerance));
            Assert.That(rig.Sink.Count<SetBodyMoveCommand>(), Is.Zero);
        }

        [Test]
        public void ASwipeThatDriftsIntoTheWeaponZone_NeverDraws()
        {
            var rig = new SchemeRig();

            rig.Tick(Down(1, BodySpot, 0.0));
            rig.Tick(Move(1, Near(BodySpot, 400f, 20f), 1.0));
            rig.Tick(Up(1, Near(BodySpot, 800f, 20f), 2.0));

            Assert.That(rig.Sink.Count<SetPathCommand>(), Is.Zero);
            Assert.That(rig.Scheme.ActiveStroke, Is.Empty);
        }

        [Test]
        public void OnlyOneFingerDrawsAtATime_ASecondOneInTheWeaponZoneIsIgnored()
        {
            var rig = new SchemeRig();
            Vec2 second = new Vec2(1400f, 600f);

            rig.Tick(Down(1, WeaponSpot, 0.0), Down(2, second, 0.0));
            rig.Tick(Move(1, Near(WeaponSpot, 100f, 0f), 0.1), Move(2, Near(second, 100f, 0f), 0.1));
            rig.Tick(Up(2, Near(second, 100f, 0f), 0.2));
            Assert.That(rig.Sink.Commands, Is.Empty, "the ignored finger sent nothing");
            Assert.That(rig.Scheme.ActiveStroke.Count, Is.GreaterThan(1), "the first finger is still drawing");

            rig.Tick(Up(1, Near(WeaponSpot, 100f, 0f), 0.3));
            Vec2 drawnEnd = rig.Sink.Only<SetPathCommand>().Path.Points[rig.Sink.Only<SetPathCommand>().Path.Points.Count - 1];
            Assert.That(Vec2.Distance(drawnEnd, rig.LocalOf(Near(WeaponSpot, 100f, 0f))), Is.LessThan(PointTolerance));
        }

        [Test]
        public void ATouchTheSystemCancels_SendsNothing()
        {
            var rig = new SchemeRig();

            rig.Tick(Down(1, WeaponSpot, 0.0));
            rig.Tick(Move(1, Near(WeaponSpot, 200f, 0f), 0.1));
            rig.Tick(Cancel(1, Near(WeaponSpot, 200f, 0f), 0.2));

            Assert.That(rig.Sink.Commands, Is.Empty);
            Assert.That(rig.Scheme.ActiveStroke, Is.Empty);
        }

        // ----- the body zone -----

        [Test]
        public void ASwipe_SetsTheBodyMoveAtOnce_AndNothingMoreWhileTheFingerKeepsGoing()
        {
            var rig = new SchemeRig();

            rig.Tick(Down(1, BodySpot, 0.00));
            rig.Tick(Move(1, Near(BodySpot, 15f, 0f), 0.03));
            Assert.That(rig.Sink.Commands, Is.Empty, "not far enough yet");

            rig.Tick(Move(1, Near(BodySpot, 45f, 2f), 0.06));
            Assert.That(rig.Sink.Only<SetBodyMoveCommand>().Move, Is.EqualTo(BodyMove.Lunge), "forward is toward the screen's right");

            rig.Tick(Move(1, Near(BodySpot, 200f, 0f), 0.09));
            rig.Tick(Up(1, Near(BodySpot, 200f, 0f), 0.12));
            Assert.That(rig.Sink.Commands.Count, Is.EqualTo(1));
        }

        [TestCase(0f, 60f, BodyMove.Jump)]
        [TestCase(0f, -60f, BodyMove.Crouch)]
        [TestCase(-60f, 0f, BodyMove.StepBack)]
        public void TheOtherDirections(float dx, float dy, BodyMove expected)
        {
            var rig = new SchemeRig();

            rig.Tick(Down(1, Near(BodySpot, 40f, 0f), 0.0));
            rig.Tick(Move(1, Near(BodySpot, 40f + dx, dy), 0.08));

            Assert.That(rig.Sink.Only<SetBodyMoveCommand>().Move, Is.EqualTo(expected));
        }

        [Test]
        public void ADragThatIsTooSlow_IsNotASwipe_AndNotATap()
        {
            var rig = new SchemeRig();

            rig.Tick(Down(1, BodySpot, 0.0));
            rig.Tick(Move(1, Near(BodySpot, 20f, 0f), 0.2));
            rig.Tick(Move(1, Near(BodySpot, 100f, 0f), 0.5));
            rig.Tick(Up(1, Near(BodySpot, 100f, 0f), 0.6));

            Assert.That(rig.Sink.Commands, Is.Empty);
        }

        [Test]
        public void ADiagonalFlick_DoesNothing()
        {
            var rig = new SchemeRig();

            rig.Tick(Down(1, BodySpot, 0.0));
            rig.Tick(Move(1, Near(BodySpot, 60f, 60f), 0.1));
            rig.Tick(Up(1, Near(BodySpot, 60f, 60f), 0.15));

            Assert.That(rig.Sink.Commands, Is.Empty);
        }

        [Test]
        public void AQuickTapInTheBodyZone_ClearsTheBodyMove()
        {
            var rig = new SchemeRig();

            rig.Tick(Down(1, BodySpot, 0.0));
            rig.Tick(Up(1, Near(BodySpot, 2f, 1f), 0.1));

            Assert.That(rig.Sink.Only<SetBodyMoveCommand>().Move, Is.EqualTo(BodyMove.None));
        }

        [Test]
        public void TheTapOption_CanBeSwitchedOff()
        {
            var rig = new SchemeRig(tapClearsMove: false);

            rig.Tick(Down(1, BodySpot, 0.0));
            rig.Tick(Up(1, BodySpot, 0.1));

            Assert.That(rig.Sink.Commands, Is.Empty);
        }

        [Test]
        public void AFingerRestingInTheBodyZone_IsNotATap()
        {
            var rig = new SchemeRig();

            rig.Tick(Down(1, BodySpot, 0.0));
            rig.Tick(Up(1, BodySpot, 1.0));

            Assert.That(rig.Sink.Commands, Is.Empty);
        }

        [Test]
        public void ASwipeThatAlreadyFired_IsNotAlsoATap()
        {
            var rig = new SchemeRig();

            rig.Tick(Down(1, BodySpot, 0.0));
            rig.Tick(Move(1, Near(BodySpot, 50f, 0f), 0.05));
            rig.Tick(Move(1, BodySpot, 0.1));
            rig.Tick(Up(1, BodySpot, 0.15));

            Assert.That(rig.Sink.Commands.Count, Is.EqualTo(1));
        }

        // ----- both thumbs, the HUD, handedness -----

        [Test]
        public void TwoThumbs_ASwipeInTheBodyZoneWhileDrawingInTheWeaponZone()
        {
            var rig = new SchemeRig();

            rig.Tick(Down(1, WeaponSpot, 0.0), Down(2, BodySpot, 0.0));
            rig.Tick(Move(1, Near(WeaponSpot, 120f, 30f), 0.05), Move(2, Near(BodySpot, 0f, -50f), 0.05));
            rig.Tick(Up(1, Near(WeaponSpot, 240f, 0f), 0.1), Up(2, Near(BodySpot, 0f, -50f), 0.1));

            Assert.That(rig.Sink.Only<SetBodyMoveCommand>().Move, Is.EqualTo(BodyMove.Crouch));
            Assert.That(rig.Sink.Only<SetPathCommand>().Path.Points.Count, Is.GreaterThan(1));
        }

        [Test]
        public void AFingerThatLandsOnTheHud_DoesNothing_EvenAfterItSlidesOff()
        {
            var hud = new FixedBlocker(new Vec2(1500f, 900f), new Vec2(1900f, 1080f));
            var rig = new SchemeRig(blocker: hud);
            Vec2 onHud = new Vec2(1700f, 1000f);

            rig.Tick(Down(1, onHud, 0.0));
            rig.Tick(Move(1, new Vec2(900f, 500f), 0.1));
            rig.Tick(Up(1, new Vec2(900f, 500f), 0.2));

            Assert.That(rig.Sink.Commands, Is.Empty);
            Assert.That(rig.Scheme.ActiveStroke, Is.Empty);
        }

        [Test]
        public void ATouchOutsideTheHud_StillDraws()
        {
            var rig = new SchemeRig(blocker: new FixedBlocker(new Vec2(1500f, 900f), new Vec2(1900f, 1080f)));

            rig.Tick(Down(1, WeaponSpot, 0.0));
            rig.Tick(Up(1, WeaponSpot, 0.1));

            Assert.That(rig.Sink.Count<SetPathCommand>(), Is.EqualTo(1));
        }

        [Test]
        public void LeftHanded_TheBodyZoneIsOnTheRight_AndForwardIsTowardTheScreensLeft()
        {
            var rig = new SchemeRig(mirrored: true);
            var bodySpot = new Vec2(1800f, 540f);

            rig.Tick(Down(1, bodySpot, 0.0));
            rig.Tick(Move(1, Near(bodySpot, -60f, 0f), 0.08));
            Assert.That(rig.Sink.Only<SetBodyMoveCommand>().Move, Is.EqualTo(BodyMove.Lunge));

            rig.Tick(Up(1, Near(bodySpot, -60f, 0f), 0.1));
            rig.Tick(Down(2, new Vec2(100f, 540f), 1.0), Up(2, new Vec2(100f, 540f), 1.05));
            Assert.That(rig.Sink.Count<SetPathCommand>(), Is.EqualTo(1), "the left edge is the weapon zone now: that tap raised an item");
        }

        [TestCase(false, Side.Left)]
        [TestCase(false, Side.Right)]
        [TestCase(true, Side.Left)]
        [TestCase(true, Side.Right)]
        public void ADrawnShapeComesOutInTheFightersFrame_InEveryLayout(bool mirrored, Side side)
        {
            var rig = new SchemeRig(mirrored, side);

            rig.Play(rig.Stroke(1, WeavingThrust, 0.4f, 60f), 60f);

            WeaponPath sent = rig.Sink.Only<SetPathCommand>().Path;
            Assert.That(sent.Points.Count, Is.GreaterThan(10), "one point per reading, and the readings are well apart");
            Assert.That(Vec2.Distance(sent.Points[0], new Vec2(100f, 100f)), Is.LessThan(1.5f), "starts at the shape's start");
            Assert.That(Vec2.Distance(Last(sent), new Vec2(400f, 100f)), Is.LessThan(1.5f), "forward is +X whichever way the screen faces");
        }

        // ----- life cycle -----

        [Test]
        public void AfterEnd_NothingIsRead()
        {
            var rig = new SchemeRig();
            rig.Scheme.End();

            rig.Tick(Down(1, WeaponSpot, 0.0), Up(1, WeaponSpot, 0.1));

            Assert.That(rig.Sink.Commands, Is.Empty);
            Assert.That(rig.Touches.PendingFrames, Is.EqualTo(1), "the touches were not even looked at");
        }

        [Test]
        public void ANewLayout_DropsTheFingersThatWereDown()
        {
            var rig = new SchemeRig();
            rig.Tick(Down(1, WeaponSpot, 0.0));
            rig.Tick(Move(1, Near(WeaponSpot, 200f, 0f), 0.1));
            Assert.That(rig.Scheme.ActiveStroke, Is.Not.Empty);

            rig.Scheme.SetLayout(new ScreenLayout(Width, Height, BodyZoneFraction, true, Side.Left));
            rig.Tick(Up(1, Near(WeaponSpot, 200f, 0f), 0.2));

            Assert.That(rig.Scheme.ActiveStroke, Is.Empty);
            Assert.That(rig.Sink.Commands, Is.Empty);
        }

        [Test]
        public void CancelGestures_ForgetsTheStrokeInProgress()
        {
            var rig = new SchemeRig();
            rig.Tick(Down(1, WeaponSpot, 0.0));
            rig.Tick(Move(1, Near(WeaponSpot, 200f, 0f), 0.1));

            rig.Scheme.CancelGestures();
            rig.Tick(Up(1, Near(WeaponSpot, 200f, 0f), 0.2));

            Assert.That(rig.Sink.Commands, Is.Empty);
        }

        [Test]
        public void TheWindowChangingSize_MidStroke_KeepsDrawingInTheNewScale()
        {
            var rig = new SchemeRig();
            rig.Tick(Down(1, WeaponSpot, 0.0));

            rig.Touches.ScreenSizePixels = new Vec2(2400f, 1080f);
            var wider = rig.Layout.WithScreenSize(2400f, 1080f);
            var centre = new Vec2(1200f, 540f);
            rig.Tick(Move(1, centre, 0.1));
            rig.Tick(Up(1, centre, 0.2));

            WeaponPath sent = rig.Sink.Only<SetPathCommand>().Path;
            Assert.That(Vec2.Distance(Last(sent), rig.LocalOf(centre, wider)), Is.LessThan(PointTolerance));
        }

        [Test]
        public void ARefusedCommandIsNotSentAgain()
        {
            var rig = new SchemeRig();
            rig.Sink.Result = CommandResult.Reject(CommandRejection.AlreadyReady);

            rig.Tick(Down(1, WeaponSpot, 0.0));
            rig.Tick(Up(1, WeaponSpot, 0.1));
            rig.Tick();

            Assert.That(rig.Sink.Count<SetPathCommand>(), Is.EqualTo(1));
        }
    }
}
