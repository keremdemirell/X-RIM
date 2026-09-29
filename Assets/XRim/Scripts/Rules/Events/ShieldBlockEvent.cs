using XRim.Core;
using XRim.Rules.Combat;

namespace XRim.Rules.Events
{
    public sealed class ShieldBlockEvent : MatchEvent
    {
        public Side Blocker { get; }
        public BlockResult Result { get; }

        public ShieldBlockEvent(SimTime time, Side blocker, BlockResult result) : base(time)
        {
            Blocker = blocker;
            Result = result;
        }
    }
}
