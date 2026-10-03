using System.Collections.Generic;
using XRim.Core;
using XRim.Rules.Events;

namespace XRim.Rules.Combat
{
    /// <summary>What a shield block did to the turn, for the simulation to apply (the weapon bounces off, the holder is pushed back) and record.</summary>
    public sealed class BlockResolution
    {
        public SimTime Time { get; }
        public Side Blocker { get; }
        public Side Attacker => Blocker.Opponent();
        public BlockResult Result { get; }

        /// <summary>Domain events to record, all at <see cref="Time"/>: the block, then a stagger if there is one.</summary>
        public IReadOnlyList<MatchEvent> Events { get; }

        public BlockResolution(SimTime time, Side blocker, BlockResult result, IReadOnlyList<MatchEvent> events)
        {
            Time = time;
            Blocker = blocker;
            Result = result;
            Events = Guard.NotNull(events, nameof(events));
        }
    }
}
