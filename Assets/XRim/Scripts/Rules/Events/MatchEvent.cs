using XRim.Core;

namespace XRim.Rules.Events
{
    /// <summary>
    /// A domain event produced while resolving a turn, stamped with simulation time. Events are data, not
    /// C# events: the recorded list drives VFX, audio and replays, and tests can assert on it.
    /// </summary>
    public abstract class MatchEvent
    {
        public SimTime Time { get; }

        protected MatchEvent(SimTime time)
        {
            Time = time;
        }
    }
}
