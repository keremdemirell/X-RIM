using XRim.Core;

namespace XRim.Presentation.Feel
{
    /// <summary>
    /// One slowed stretch of a turn, in turn time: full slowness from <see cref="Start"/> to <see cref="End"/>, with the speed easing
    /// down from real time over the ramp before it and back up over the ramp after it. <see cref="Moment"/> is what it slows down for.
    /// </summary>
    public readonly struct SlowMotionWindow
    {
        public SimTime Moment { get; }
        public SimTime Start { get; }
        public SimTime End { get; }

        /// <summary>Playback speed through the stretch (0..1).</summary>
        public float Speed { get; }

        /// <summary>Seconds of the turn spent easing in and out.</summary>
        public float RampSeconds { get; }

        public SlowMotionWindow(SimTime moment, SimTime start, SimTime end, float speed, float rampSeconds)
        {
            Moment = moment;
            Start = start;
            End = end;
            Speed = speed;
            RampSeconds = rampSeconds > 0f ? rampSeconds : 0f;
        }

        /// <summary>Where the slowing starts: the ramp's beginning.</summary>
        public double EaseInFromSeconds => Start.Seconds - RampSeconds;

        /// <summary>Where real time is back: the ramp's end.</summary>
        public double EaseOutUntilSeconds => End.Seconds + RampSeconds;

        /// <summary>The playback speed this stretch asks for at a time of the turn (1 outside it).</summary>
        public float SpeedAt(double seconds)
        {
            double start = Start.Seconds;
            double end = End.Seconds;
            if (seconds >= start && seconds <= end) return Speed;
            if (RampSeconds <= 0f) return 1f;
            if (seconds < start && seconds >= start - RampSeconds) return Lerp(1f, Speed, (seconds - (start - RampSeconds)) / RampSeconds);
            if (seconds > end && seconds <= end + RampSeconds) return Lerp(Speed, 1f, (seconds - end) / RampSeconds);
            return 1f;
        }

        /// <summary>True when the turn just after this time is easing in or out (the speed changes from instant to instant).</summary>
        public bool IsRampingAt(double seconds) =>
            RampSeconds > 0f && (seconds >= EaseInFromSeconds && seconds < Start.Seconds || seconds >= End.Seconds && seconds < EaseOutUntilSeconds);

        private static float Lerp(float from, float to, double t) => (float)(from + (to - from) * t);
    }
}
