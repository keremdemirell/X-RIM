using System;
using XRim.Config;
using XRim.Presentation.Playback;
using XRim.Rules.Events;

namespace XRim.Presentation.Feel
{
    /// <summary>
    /// Game-feel layer (GDD pillar 1). It only bends playback, so it can be pushed hard without changing outcomes. Now: a hitstop on
    /// every landed hit (<see cref="FeelConfig.HitStopSeconds"/>, read live so tuning edits show on the next hit). Slow motion on severs
    /// and KOs and the camera punch come in Session 13.
    /// </summary>
    public sealed class FeelDirector : IMatchEventReactor, IPlaybackPacer
    {
        private readonly HitStop _hitStop = new HitStop();

        private FeelConfig Config { get; }

        public FeelDirector(FeelConfig config)
        {
            Config = config != null ? config : throw new ArgumentNullException(nameof(config));
        }

        public bool IsHolding => _hitStop.IsHolding;

        public void OnMatchEvent(MatchEvent matchEvent)
        {
            if (matchEvent is HitLandedEvent) _hitStop.Trigger(Config.HitStopSeconds);
        }

        public float Pace(float realDeltaSeconds) => _hitStop.Pace(realDeltaSeconds);

        public void Reset() => _hitStop.Reset();
    }
}
