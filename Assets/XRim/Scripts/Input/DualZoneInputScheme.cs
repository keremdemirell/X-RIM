using System;
using System.Collections.Generic;
using XRim.Core;
using XRim.Networking;
using XRim.Rules;
using XRim.Rules.Paths;
using XRim.Rules.Planning;
using XRim.Simulation.Drivers;
using XRim.Simulation.Physics;

namespace XRim.Input
{
    /// <summary>
    /// GDD §4: a small body zone over the player's own dummy (swipes → body move) and a large weapon zone (drawing →
    /// weapon path). No virtual D-pad or direction buttons (Rejected). Only emits commands; the rules decide.
    /// <list type="bullet">
    /// <item>A finger belongs to the zone it lands in for as long as it is down, so a stroke that drifts over the body zone keeps
    /// drawing. A finger that lands on a HUD control (<see cref="ITouchBlocker"/>) does nothing at all.</item>
    /// <item>Body zone: a flick the <see cref="ISwipeClassifier"/> recognises sets the body move the moment it is recognised (no
    /// wait for the finger to lift); the rest of that finger is ignored. A quick tap sets it back to neutral
    /// (<see cref="DualZoneSettings.TapInBodyZoneClearsMove"/>, flagged: the scheme's feel is TBD).</item>
    /// <item>Weapon zone: one finger draws one continuous stroke (D5); a second finger there is ignored while it draws. The stroke
    /// is sent when the finger lifts, so the new one replaces the old only then. A tap, a finger that did not move, is a
    /// one-point stroke: the rules add the lead-in (D3), which raises a shield where it was tapped (§7). A touch the system
    /// cancels sends nothing.</item>
    /// </list>
    /// Points are converted to the torso frame in arena units as they arrive; positions are kept, times are not (§4 input fairness).
    /// </summary>
    public sealed class DualZoneInputScheme : IInputScheme
    {
        /// <summary>Raw points closer than this are skipped; the path rules resample anyway.</summary>
        private const float MinStrokePointSpacingUnits = 1f;

        private const int NoFinger = int.MinValue;

        private enum GestureKind
        {
            Ignored,
            Body,
            Stroke,
        }

        private sealed class Gesture
        {
            public GestureKind Kind;
            public Vec2 StartPixels;
            public double StartSeconds;
            public float MaxTravelPixels;

            /// <summary>A body swipe that already fired or ran out of time.</summary>
            public bool Settled;
        }

        private readonly ITouchSource _touches;
        private readonly ISwipeClassifier _swipes;
        private readonly SwipeSettings _swipeSettings;
        private readonly DualZoneSettings _settings;
        private readonly ScreenToArenaMapper _mapper;
        private readonly Func<ArenaViewFrame> _view;
        private readonly ITouchBlocker _blocker;
        private readonly Dictionary<int, Gesture> _gestures = new Dictionary<int, Gesture>();
        private readonly List<TouchSample> _samples = new List<TouchSample>();
        private readonly List<Vec2> _stroke = new List<Vec2>();

        private PlanningWindow _window;
        private IPlanningCommandSink _sink;
        private ScreenLayout _layout;
        private Side _side;
        private BodyPose _root;
        private int _drawingFinger;
        private bool _active;

        /// <param name="swipeSettings">The same numbers the classifier was built with; its window also times a body tap.</param>
        /// <param name="view">What the screen shows now (the camera may move), read once per frame with touches.</param>
        /// <param name="blocker">Where the HUD sits; null for none.</param>
        public DualZoneInputScheme(ITouchSource touches, ISwipeClassifier swipes, SwipeSettings swipeSettings, DualZoneSettings settings,
            ScreenToArenaMapper mapper, Func<ArenaViewFrame> view, ITouchBlocker blocker = null)
        {
            _touches = Guard.NotNull(touches, nameof(touches));
            _swipes = Guard.NotNull(swipes, nameof(swipes));
            _swipeSettings = swipeSettings;
            _settings = settings;
            _mapper = Guard.NotNull(mapper, nameof(mapper));
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _blocker = blocker;
            _drawingFinger = NoFinger;
        }

        public IReadOnlyList<Vec2> ActiveStroke => _stroke;

        public void Begin(PlanningWindow window, ScreenLayout layout, IPlanningCommandSink sink)
        {
            _window = Guard.NotNull(window, nameof(window));
            _sink = Guard.NotNull(sink, nameof(sink));
            _layout = Guard.NotNull(layout, nameof(layout));
            FollowLayoutSide();
            ClearGestures();
            _active = true;
        }

        public void SetLayout(ScreenLayout layout)
        {
            _layout = Guard.NotNull(layout, nameof(layout));
            FollowLayoutSide();
            ClearGestures();
        }

        public void CancelGestures() => ClearGestures();

        public void End()
        {
            _active = false;
            ClearGestures();
            _sink = null;
        }

        public void Tick()
        {
            if (!_active) return;

            FollowScreenSize();
            _samples.Clear();
            _touches.ReadInto(_samples);
            if (_samples.Count == 0) return;

            ArenaViewFrame view = _view();
            for (int i = 0; i < _samples.Count; i++)
            {
                Handle(_samples[i], view);
            }
        }

        private void Handle(TouchSample sample, ArenaViewFrame view)
        {
            switch (sample.Phase)
            {
                case TouchSamplePhase.Began:
                    Land(sample, view);
                    break;
                case TouchSamplePhase.Moved:
                    if (_gestures.TryGetValue(sample.FingerId, out Gesture moving)) Move(moving, sample, view);
                    break;
                case TouchSamplePhase.Ended:
                case TouchSamplePhase.Canceled:
                    if (_gestures.TryGetValue(sample.FingerId, out Gesture lifted))
                    {
                        Lift(lifted, sample, sample.Phase == TouchSamplePhase.Ended, view);
                        _gestures.Remove(sample.FingerId);
                    }

                    break;
            }
        }

        private void Land(TouchSample sample, ArenaViewFrame view)
        {
            // A finger id coming back without having lifted: drop what it was doing.
            if (_gestures.Remove(sample.FingerId) && _drawingFinger == sample.FingerId) StopDrawing();

            var gesture = new Gesture { StartPixels = sample.PositionPixels, StartSeconds = sample.TimeSeconds };
            _gestures[sample.FingerId] = gesture;

            if (_blocker != null && _blocker.Blocks(sample.PositionPixels))
            {
                gesture.Kind = GestureKind.Ignored;
            }
            else if (_layout.ZoneAt(sample.PositionPixels) == ScreenZone.Body)
            {
                gesture.Kind = GestureKind.Body;
            }
            else if (_drawingFinger == NoFinger)
            {
                gesture.Kind = GestureKind.Stroke;
                _drawingFinger = sample.FingerId;
                _stroke.Clear();
                _stroke.Add(ToTorsoFrame(sample.PositionPixels, view));
            }
            else
            {
                gesture.Kind = GestureKind.Ignored; // one continuous stroke at a time (D5)
            }
        }

        private void Move(Gesture gesture, TouchSample sample, ArenaViewFrame view)
        {
            gesture.MaxTravelPixels = Math.Max(gesture.MaxTravelPixels, Vec2.Distance(gesture.StartPixels, sample.PositionPixels));
            switch (gesture.Kind)
            {
                case GestureKind.Body:
                    ClassifySwipe(gesture, sample);
                    break;
                case GestureKind.Stroke:
                    Vec2 point = ToTorsoFrame(sample.PositionPixels, view);
                    if (Vec2.Distance(_stroke[_stroke.Count - 1], point) >= MinStrokePointSpacingUnits) _stroke.Add(point);
                    break;
            }
        }

        private void ClassifySwipe(Gesture gesture, TouchSample sample)
        {
            if (gesture.Settled) return;

            float seconds = (float)(sample.TimeSeconds - gesture.StartSeconds);
            if (seconds > _swipeSettings.WindowSeconds)
            {
                gesture.Settled = true; // too slow to be a swipe
                return;
            }

            BodyMove? move = _swipes.Classify(gesture.StartPixels, sample.PositionPixels, seconds, _layout);
            if (move.HasValue)
            {
                gesture.Settled = true;
                _sink.Send(new SetBodyMoveCommand(move.Value));
            }
        }

        private void Lift(Gesture gesture, TouchSample sample, bool deliberate, ArenaViewFrame view)
        {
            gesture.MaxTravelPixels = Math.Max(gesture.MaxTravelPixels, Vec2.Distance(gesture.StartPixels, sample.PositionPixels));
            bool tapped = gesture.MaxTravelPixels <= _settings.TapMaxTravelScreenFraction * _layout.ScreenHeightPixels;
            switch (gesture.Kind)
            {
                case GestureKind.Body:
                    float seconds = (float)(sample.TimeSeconds - gesture.StartSeconds);
                    if (deliberate && tapped && !gesture.Settled && seconds <= _settings.TapMaxSeconds && _settings.TapInBodyZoneClearsMove)
                    {
                        _sink.Send(new SetBodyMoveCommand(BodyMove.None));
                    }

                    break;
                case GestureKind.Stroke:
                    if (deliberate)
                    {
                        // The spacing filter may have skipped the last readings: where the finger lifted is where the stroke ends.
                        Vec2 end = ToTorsoFrame(sample.PositionPixels, view);
                        if (!tapped && end != _stroke[_stroke.Count - 1]) _stroke.Add(end);
                        SendStroke(tapped);
                    }

                    StopDrawing();
                    break;
            }
        }

        /// <summary>A finger that did not move sends just the spot it touched: the tap.</summary>
        private void SendStroke(bool tapped)
        {
            var points = new List<Vec2>(tapped ? 1 : _stroke.Count);
            if (tapped) points.Add(_stroke[0]);
            else points.AddRange(_stroke);
            _sink.Send(new SetPathCommand(new WeaponPath(points)));
        }

        private void StopDrawing()
        {
            _drawingFinger = NoFinger;
            _stroke.Clear();
        }

        private Vec2 ToTorsoFrame(Vec2 screenPixels, ArenaViewFrame view) =>
            _mapper.ToFighterFrame(screenPixels, _layout, view, _root, _side);

        private void ClearGestures()
        {
            _gestures.Clear();
            StopDrawing();
        }

        private void FollowLayoutSide()
        {
            _side = _layout.OwnSide;
            if (_window != null) _root = TurnStartRoot.Of(_window.Board.Pose.Get(_side));
        }

        private void FollowScreenSize()
        {
            Vec2 size = _touches.ScreenSizePixels;
            bool usable = size.X > 0f && size.Y > 0f;
            if (usable && (size.X != _layout.ScreenWidthPixels || size.Y != _layout.ScreenHeightPixels))
            {
                _layout = _layout.WithScreenSize(size.X, size.Y);
            }
        }
    }
}
