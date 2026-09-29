using XRim.Core;

namespace XRim.Rules.Events
{
    /// <summary>A dummy hit before its own attack landed has that attack cancelled (GDD §9).</summary>
    public sealed class AttackInterruptedEvent : MatchEvent
    {
        public Side Interrupted { get; }

        public AttackInterruptedEvent(SimTime time, Side interrupted) : base(time)
        {
            Interrupted = interrupted;
        }
    }
}
