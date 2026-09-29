using XRim.Core;
using XRim.Rules.Status;

namespace XRim.Rules.Events
{
    public sealed class StatusAppliedEvent : MatchEvent
    {
        public Side Side { get; }
        public StatusKind Kind { get; }

        public StatusAppliedEvent(SimTime time, Side side, StatusKind kind) : base(time)
        {
            Side = side;
            Kind = kind;
        }
    }
}
