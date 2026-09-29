using XRim.Core;

namespace XRim.Rules.Events
{
    /// <summary>The limb falls with bolts, nuts and hydraulic fluid and stays on the floor (GDD §2, §12).</summary>
    public sealed class LimbSeveredEvent : MatchEvent
    {
        public Side Side { get; }
        public BodyPart Part { get; }

        public LimbSeveredEvent(SimTime time, Side side, BodyPart part) : base(time)
        {
            Side = side;
            Part = part;
        }
    }
}
