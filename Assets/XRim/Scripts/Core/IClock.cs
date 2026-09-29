namespace XRim.Core
{
    /// <summary>
    /// Wall-clock time as seen by whoever owns the match: the planning timer, the lock-out window
    /// and Ready timestamps. This is not the simulation clock; see <see cref="SimTime"/>.
    /// </summary>
    public interface IClock
    {
        double NowSeconds { get; }
    }
}
