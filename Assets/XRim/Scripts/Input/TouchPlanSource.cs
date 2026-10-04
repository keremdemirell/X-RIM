using System;
using System.Collections.Generic;
using XRim.Core;
using XRim.Networking;
using XRim.Rules.Planning;

namespace XRim.Input
{
    /// <summary>
    /// A human on this device, planning through an <see cref="IInputScheme"/>. Everything it does reaches the rules as the same
    /// <see cref="PlanningCommand"/>s a remote player would send: the touch scheme sends strokes and body moves, the HUD sends
    /// the weapon and Ready through <see cref="Send"/>. <see cref="Draft"/> keeps what the rules accepted for the preview and the HUD.
    /// </summary>
    public sealed class TouchPlanSource : IPlanSource
    {
        private readonly IInputScheme _scheme;
        private ScreenLayout _layout;
        private IPlanningCommandSink _sink;
        private bool _isActive = true;

        public Side Side { get; }

        /// <summary>What this device has set this turn.</summary>
        public PlanDraft Draft { get; } = new PlanDraft();

        /// <summary>The planning phase in progress; null between phases.</summary>
        public PlanningWindow Window { get; private set; }

        public bool IsPlanning => Window != null;

        public ScreenLayout Layout
        {
            get => _layout;
            set
            {
                _layout = Guard.NotNull(value, nameof(value));
                if (_layout.OwnSide != Side) throw new ArgumentException("The layout belongs to the other side.", nameof(value));
                _scheme.SetLayout(_layout);
            }
        }

        /// <summary>
        /// Whether touches reach this source. Off for the side nobody is playing on this device (the sandbox's hot-seat toggle);
        /// switching it off drops the fingers that were down.
        /// </summary>
        public bool IsActive
        {
            get => _isActive;
            set
            {
                if (_isActive == value) return;
                _isActive = value;
                if (!_isActive) _scheme.CancelGestures();
            }
        }

        /// <summary>The stroke being drawn right now (torso frame), for the live preview; empty when no finger is drawing.</summary>
        public IReadOnlyList<Vec2> ActiveStroke => _scheme.ActiveStroke;

        public TouchPlanSource(Side side, IInputScheme scheme, ScreenLayout layout)
        {
            Side = side;
            _scheme = Guard.NotNull(scheme, nameof(scheme));
            _layout = Guard.NotNull(layout, nameof(layout));
            if (layout.OwnSide != side) throw new ArgumentException("The layout belongs to the other side.", nameof(layout));
        }

        public void OnPlanningStarted(PlanningWindow window, IPlanningCommandSink sink)
        {
            Window = Guard.NotNull(window, nameof(window));
            Draft.Reset(window.Board.State.Fighters[Side].CurrentWeapon);
            _sink = new DraftTrackingSink(Guard.NotNull(sink, nameof(sink)), Draft);
            _scheme.Begin(window, _layout, _sink);
        }

        /// <summary>Call once per frame.</summary>
        public void Tick()
        {
            if (IsPlanning && _isActive) _scheme.Tick();
        }

        /// <summary>A command from the HUD (weapon, Ready), through the same tracking as the touches.</summary>
        public CommandResult Send(PlanningCommand command)
        {
            Guard.NotNull(command, nameof(command));
            return _sink != null ? _sink.Send(command) : CommandResult.Reject(CommandRejection.NotInPlanningPhase);
        }

        public void OnPlanningEnded()
        {
            Window = null;
            _sink = null;
            _scheme.End();
        }
    }
}
