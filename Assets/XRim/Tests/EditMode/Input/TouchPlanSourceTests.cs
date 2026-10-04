using System;
using System.Collections.Generic;
using NUnit.Framework;
using XRim.Core;
using XRim.Input;
using XRim.Networking;
using XRim.Rules;
using XRim.Rules.Paths;
using XRim.Rules.Planning;
using static XRim.Tests.EditMode.Input.SchemeRig;

namespace XRim.Tests.EditMode.Input
{
    /// <summary>
    /// A human on this device: the touches and the HUD both reach the rules as commands, and the draft keeps only what the rules
    /// accepted.
    /// </summary>
    public sealed class TouchPlanSourceTests
    {
        private static readonly Vec2 WeaponSpot = new Vec2(900f, 400f);

        /// <summary>A scheme that only records how it was called.</summary>
        private sealed class SpyScheme : IInputScheme
        {
            public int Ticks { get; private set; }
            public int Cancels { get; private set; }
            public int Begins { get; private set; }
            public int Ends { get; private set; }
            public ScreenLayout LastLayout { get; private set; }
            public IPlanningCommandSink Sink { get; private set; }
            public IReadOnlyList<Vec2> ActiveStroke { get; } = new List<Vec2> { new Vec2(1f, 2f) };

            public void Begin(PlanningWindow window, ScreenLayout layout, IPlanningCommandSink sink)
            {
                Begins++;
                LastLayout = layout;
                Sink = sink;
            }

            public void Tick() => Ticks++;

            public void SetLayout(ScreenLayout layout) => LastLayout = layout;

            public void CancelGestures() => Cancels++;

            public void End() => Ends++;
        }

        private static ScreenLayout Layout(Side side = Side.Left, bool mirrored = false) =>
            new ScreenLayout(Width, Height, BodyZoneFraction, mirrored, side);

        private static TouchPlanSource Source(IInputScheme scheme, Side side = Side.Left) => new TouchPlanSource(side, scheme, Layout(side));

        private SchemeRig _window;

        [SetUp]
        public void SetUp() => _window = new SchemeRig();

        [Test]
        public void PlanningStarting_ResetsTheDraftToTheWeaponInHand_AndStartsTheScheme()
        {
            var scheme = new SpyScheme();
            TouchPlanSource source = Source(scheme);

            source.OnPlanningStarted(_window.Window, _window.Sink);

            Assert.That(source.IsPlanning, Is.True);
            Assert.That(source.Draft.Weapon, Is.EqualTo(_window.Window.Board.State.Fighters[Side.Left].CurrentWeapon));
            Assert.That(scheme.Begins, Is.EqualTo(1));
            Assert.That(scheme.LastLayout, Is.SameAs(source.Layout));
        }

        [Test]
        public void TouchesAreOnlyReadWhilePlanning_AndWhileActive()
        {
            var scheme = new SpyScheme();
            TouchPlanSource source = Source(scheme);

            source.Tick();
            Assert.That(scheme.Ticks, Is.Zero, "no planning phase yet");

            source.OnPlanningStarted(_window.Window, _window.Sink);
            source.Tick();
            Assert.That(scheme.Ticks, Is.EqualTo(1));

            source.IsActive = false;
            source.Tick();
            Assert.That(scheme.Ticks, Is.EqualTo(1), "the other side of a hot-seat device");
            Assert.That(scheme.Cancels, Is.EqualTo(1), "switching off drops the fingers that were down");

            source.IsActive = true;
            source.Tick();
            Assert.That(scheme.Ticks, Is.EqualTo(2));

            source.OnPlanningEnded();
            source.Tick();
            Assert.That(scheme.Ticks, Is.EqualTo(2));
            Assert.That(scheme.Ends, Is.EqualTo(1));
            Assert.That(source.IsPlanning, Is.False);
        }

        [Test]
        public void ACommandFromTheHud_GoesThroughTheRules_AndTheDraftFollowsWhatWasAccepted()
        {
            TouchPlanSource source = Source(new SpyScheme());
            source.OnPlanningStarted(_window.Window, _window.Sink);

            CommandResult accepted = source.Send(new SetBodyMoveCommand(BodyMove.Crouch));
            Assert.That(accepted.Accepted, Is.True);
            Assert.That(_window.Sink.Count<SetBodyMoveCommand>(), Is.EqualTo(1));
            Assert.That(source.Draft.BodyMove, Is.EqualTo(BodyMove.Crouch));

            _window.Sink.Result = CommandResult.Reject(CommandRejection.WeaponSwitchLockedOut);
            CommandResult refused = source.Send(new SelectWeaponCommand(WeaponIds.Mace));
            Assert.That(refused.Rejection, Is.EqualTo(CommandRejection.WeaponSwitchLockedOut));
            Assert.That(source.Draft.Weapon, Is.Not.EqualTo(WeaponIds.Mace), "the rules said no");
            Assert.That(source.Draft.LastRejection, Is.EqualTo(CommandRejection.WeaponSwitchLockedOut));
        }

        [Test]
        public void SendingOutsideAPlanningPhase_IsRefused()
        {
            TouchPlanSource source = Source(new SpyScheme());

            Assert.That(source.Send(new SetReadyCommand(true)).Rejection, Is.EqualTo(CommandRejection.NotInPlanningPhase));

            source.OnPlanningStarted(_window.Window, _window.Sink);
            source.OnPlanningEnded();
            Assert.That(source.Send(new SetReadyCommand(true)).Rejection, Is.EqualTo(CommandRejection.NotInPlanningPhase));
            Assert.That(_window.Sink.Commands, Is.Empty);
        }

        [Test]
        public void TheSchemesOwnCommandsAreTrackedToo_AStrokeEndsUpInTheDraft()
        {
            // The real scheme, on the rig's scripted touches: the stroke it sends is what the draft remembers.
            var source = new TouchPlanSource(Side.Left, _window.Scheme, _window.Layout);
            source.OnPlanningStarted(_window.Window, _window.Sink);

            _window.Tick(Down(1, WeaponSpot, 0.0));
            source.Tick();
            _window.Touches.Frame(Move(1, new Vec2(1000f, 420f), 0.1), Up(1, new Vec2(1100f, 440f), 0.2));
            source.Tick();

            Assert.That(source.Draft.Stroke.Points.Count, Is.GreaterThan(1));
            Assert.That(source.Draft.Stroke, Is.SameAs(_window.Sink.Only<SetPathCommand>().Path));
        }

        [Test]
        public void ALayoutForTheOtherSide_IsRefused()
        {
            Assert.Throws<ArgumentException>(() => new TouchPlanSource(Side.Left, new SpyScheme(), Layout(Side.Right)));

            TouchPlanSource source = Source(new SpyScheme());
            Assert.Throws<ArgumentException>(() => source.Layout = Layout(Side.Right));
        }

        [Test]
        public void ANewLayout_ReachesTheScheme()
        {
            var scheme = new SpyScheme();
            TouchPlanSource source = Source(scheme);
            ScreenLayout mirrored = Layout(Side.Left, mirrored: true);

            source.Layout = mirrored;

            Assert.That(source.Layout, Is.SameAs(mirrored));
            Assert.That(scheme.LastLayout, Is.SameAs(mirrored));
        }

        [Test]
        public void TheActiveStroke_IsTheSchemes()
        {
            var scheme = new SpyScheme();
            Assert.That(Source(scheme).ActiveStroke, Is.SameAs(scheme.ActiveStroke));
        }
    }
}
