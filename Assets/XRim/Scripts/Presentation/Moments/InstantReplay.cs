using System;
using XRim.Core;
using XRim.Presentation.Cameras;
using XRim.Presentation.Feel;
using XRim.Presentation.Playback;
using XRim.Simulation.Recording;

namespace XRim.Presentation.Moments
{
    /// <summary>
    /// The instant cinematic replay of a legendary turn (GDD pillar 1; §17: a local instant replay only, designer 2026-10-11): right after
    /// the turn plays, it plays again from its stored result, from just before the moment to just after it, slower (the moment's own slow
    /// motion still slower), every effect firing again (sparks, sounds, freezes, shake), the camera closing in on the moment. A skip ends
    /// it at once. Nothing is simulated again and nothing is saved: it only shows the turn that happened.
    /// </summary>
    public sealed class InstantReplay
    {
        private readonly TurnPlayback _playback;
        private readonly FeelDirector _feel;
        private readonly ICameraDirector _camera;
        private SimTime _end;

        /// <param name="camera">Closes in on the moment; null shows the replay without the zoom.</param>
        public InstantReplay(TurnPlayback playback, FeelDirector feel, ICameraDirector camera)
        {
            _playback = Guard.NotNull(playback, nameof(playback));
            _feel = Guard.NotNull(feel, nameof(feel));
            _camera = camera;
        }

        /// <summary>The replay ended (it reached the end of its stretch, or it was skipped).</summary>
        public event Action Finished;

        public bool IsPlaying { get; private set; }

        /// <summary>The turn and moment being replayed (or last replayed).</summary>
        public TurnResult Turn { get; private set; }

        public LegendaryMoment Moment { get; private set; }

        public void Begin(TurnResult turn, LegendaryMoment moment, ReplaySettings settings)
        {
            Guard.NotNull(turn, nameof(turn));
            Guard.NotNull(settings, nameof(settings));
            Turn = turn;
            Moment = moment;
            settings.WindowAround(moment.Time, turn.Timeline.Duration, out SimTime start, out _end);
            IsPlaying = true;
            _playback.PlayFrom(turn, start);
            _feel.SlowWholeTurn(turn, settings.Speed);
            _camera?.FocusOn(moment.FocusUnits, settings.ZoomViewFraction, settings.ZoomSeconds);
        }

        /// <summary>Each frame, after playback advanced: the replay ends once it has shown its stretch and any freeze has let go.</summary>
        public void Advance()
        {
            if (!IsPlaying) return;
            if (_playback.Player.CurrentTime >= _end && !_feel.IsFrozen) End();
        }

        /// <summary>A tap: the replay ends now.</summary>
        public void Skip()
        {
            if (IsPlaying) End();
        }

        /// <summary>The match it belonged to is gone (a rematch, another mode): the replay stops without telling anyone.</summary>
        public void Cancel() => IsPlaying = false;

        private void End()
        {
            IsPlaying = false;
            Finished?.Invoke();
        }
    }
}
