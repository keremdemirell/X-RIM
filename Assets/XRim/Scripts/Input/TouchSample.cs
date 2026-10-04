using XRim.Core;

namespace XRim.Input
{
    /// <summary>
    /// One reading of one finger: where it was, when. Positions are Unity screen pixels (origin bottom-left, Y up); the time is
    /// the input system's own clock, only ever used as a difference (a swipe takes about 0.3 s, GDD §5). It never reaches a plan:
    /// finger speed cannot affect a result (GDD §4, Decided).
    /// </summary>
    public readonly struct TouchSample
    {
        public int FingerId { get; }
        public TouchSamplePhase Phase { get; }
        public Vec2 PositionPixels { get; }
        public double TimeSeconds { get; }

        public TouchSample(int fingerId, TouchSamplePhase phase, Vec2 positionPixels, double timeSeconds)
        {
            FingerId = fingerId;
            Phase = phase;
            PositionPixels = positionPixels;
            TimeSeconds = timeSeconds;
        }
    }
}
