namespace XRim.Core
{
    /// <summary>A clock that only moves when told to. Used by tests and the debug "freeze timer" mode.</summary>
    public sealed class ManualClock : IClock
    {
        public double NowSeconds { get; private set; }

        public ManualClock(double startSeconds = 0.0)
        {
            NowSeconds = startSeconds;
        }

        public void Advance(double seconds) => NowSeconds += seconds;

        public void Set(double seconds) => NowSeconds = seconds;
    }
}
