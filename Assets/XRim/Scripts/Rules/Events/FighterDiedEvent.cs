using XRim.Core;

namespace XRim.Rules.Events
{
    /// <summary>HP reached 0: the torso splits in two as the death animation (GDD §11).</summary>
    public sealed class FighterDiedEvent : MatchEvent
    {
        public Side Side { get; }

        public FighterDiedEvent(SimTime time, Side side) : base(time)
        {
            Side = side;
        }
    }
}
