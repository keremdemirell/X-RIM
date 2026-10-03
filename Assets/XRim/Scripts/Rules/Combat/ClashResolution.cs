using System.Collections.Generic;
using XRim.Core;
using XRim.Rules.Events;

namespace XRim.Rules.Combat
{
    /// <summary>What a clash did to the turn, for the simulation to apply (knock off, rebound) and record.</summary>
    public sealed class ClashResolution
    {
        public SimTime Time { get; }
        public ClashResult Result { get; }

        /// <summary>Domain events to record, all at <see cref="Time"/>: the clash, then a stagger if there is one.</summary>
        public IReadOnlyList<MatchEvent> Events { get; }

        public ClashResolution(SimTime time, ClashResult result, IReadOnlyList<MatchEvent> events)
        {
            Time = time;
            Result = result;
            Events = Guard.NotNull(events, nameof(events));
        }
    }
}
