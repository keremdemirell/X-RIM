using System;

namespace XRim.Presentation.Feel
{
    /// <summary>
    /// A short freeze of playback when a blow lands, so the hit reads and feels heavy (GDD pillar 1). Real (screen) seconds, not
    /// simulation time. Hits at the same instant share one freeze; a later hit starts its own.
    /// </summary>
    public sealed class HitStop
    {
        private float _secondsLeft;

        public bool IsHolding => _secondsLeft > 0f;

        /// <summary>Freezes playback for at least this long from now.</summary>
        public void Trigger(float seconds) => _secondsLeft = Math.Max(_secondsLeft, seconds);

        /// <summary>Uses up the freeze first; whatever real time is left this frame moves the timeline.</summary>
        public float Pace(float realDeltaSeconds)
        {
            if (_secondsLeft <= 0f) return realDeltaSeconds;

            float frozen = Math.Min(_secondsLeft, realDeltaSeconds);
            _secondsLeft -= frozen;
            return realDeltaSeconds - frozen;
        }

        public void Reset() => _secondsLeft = 0f;
    }
}
