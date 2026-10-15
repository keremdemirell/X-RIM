using System;
using System.Collections.Generic;
using XRim.Config;
using XRim.Core;
using XRim.Presentation.Cameras;
using XRim.Presentation.Playback;
using XRim.Rules.Events;
using XRim.Simulation.Recording;

namespace XRim.Presentation.Feel
{
    /// <summary>
    /// Game-feel pacing (GDD pillar 1). It only bends playback, so it can be pushed hard without changing outcomes (ARCHITECTURE §1).
    /// Session 13:
    /// <list type="bullet">
    /// <item>A <b>hitstop</b> on everything that lands, scaled by what happened (<see cref="HitStopSettings"/>: a hit by its damage and
    /// zone, a kill, a sever, a crush, a rebound, a block, a wall zap).</item>
    /// <item><b>Slow motion</b> planned when the turn starts (<see cref="SlowMotionPlanner"/>): the whole turn was simulated before it is
    /// shown, so playback starts slowing just before a sever, a death, an interrupt counter-hit or a sudden-death finish, then speeds back
    /// up.</item>
    /// <item>The <b>camera</b> (optional): it is told each turn, and the turn does not finish while it is still moving, so it is still
    /// when planning opens.</item>
    /// </list>
    /// Values come from <see cref="FeelConfig"/> at the start of each play, so tuning edits show on the next turn.
    /// </summary>
    public sealed class FeelDirector : IMatchEventReactor, IPlaybackPacer
    {
        private readonly HitStop _hitStop = new HitStop();
        private readonly ICameraDirector _camera;
        private HitStopSettings _hitStops;

        private FeelConfig Config { get; }

        /// <param name="camera">The camera to hold each turn for until it is still; null when nothing frames the view.</param>
        public FeelDirector(FeelConfig config, ICameraDirector camera = null)
        {
            Config = config != null ? config : throw new ArgumentNullException(nameof(config));
            _camera = camera;
        }

        /// <summary>The slow motion of the turn being played (debug tools show it).</summary>
        public SlowMotionPlan SlowMotion { get; private set; } = SlowMotionPlan.None;

        /// <summary>
        /// Real seconds the turn being played should take: its timeline through the slow motion, plus one freeze per instant that froze
        /// (the camera settling not counted). The per-turn log compares it with what playback really took.
        /// </summary>
        public float PlannedRealSeconds => Pacing.PlannedSeconds;

        /// <summary>The planned on-screen time of the turn being played, split into the turn, its slow motion and its freezes.</summary>
        public PacingBreakdown Pacing { get; private set; }

        public bool IsHolding => _hitStop.IsHolding || (_camera != null && _camera.IsSettling);

        /// <summary>True while a hitstop freezes playback (the instant replay waits for it before it ends).</summary>
        public bool IsFrozen => _hitStop.IsHolding;

        /// <summary>
        /// The turn being played plays at this speed at most from now on (the instant replay, Session 13); its own slow motion still goes
        /// slower where it is slower. A new <see cref="Begin"/> drops it.
        /// </summary>
        public void SlowWholeTurn(TurnResult turn, float speed)
        {
            Guard.NotNull(turn, nameof(turn));
            if (speed <= 0f || speed >= 1f) return;
            var windows = new List<SlowMotionWindow>(SlowMotion.Windows)
            {
                new SlowMotionWindow(SimTime.Zero, SimTime.Zero, turn.Timeline.Duration, speed, 0f),
            };
            SlowMotion = new SlowMotionPlan(windows);
            Pacing = PacingBreakdown.Of(turn, _hitStops ?? HitStopSettings.From(Config), SlowMotion);
        }

        public void Begin(TurnResult turn)
        {
            Guard.NotNull(turn, nameof(turn));
            _hitStop.Reset();
            _hitStops = HitStopSettings.From(Config);
            SlowMotion = SlowMotionPlanner.Plan(turn, SlowMotionSettings.From(Config));
            Pacing = PacingBreakdown.Of(turn, _hitStops, SlowMotion);
            _camera?.BeginTurn(turn);
        }

        public void OnMatchEvent(MatchEvent matchEvent)
        {
            if (_hitStops == null) _hitStops = HitStopSettings.From(Config);
            float seconds = _hitStops.SecondsFor(matchEvent);
            if (seconds > 0f) _hitStop.Trigger(seconds);
        }

        public float Pace(float realDeltaSeconds, SimTime now) => SlowMotion.Advance(now, _hitStop.Pace(realDeltaSeconds));

        /// <summary>
        /// How long a turn plays on screen: its timeline at the slow-motion plan's speed, plus the longest freeze of each instant that
        /// freezes (freezes at one instant share one, as <see cref="HitStop"/> does).
        /// </summary>
        public static float PlannedRealSecondsOf(TurnResult turn, HitStopSettings hitStops, SlowMotionPlan slowMotion) =>
            PacingBreakdown.Of(turn, hitStops, slowMotion).PlannedSeconds;
    }
}
