using System.Collections.Generic;
using XRim.Core;
using XRim.Rules.Events;

namespace XRim.Rules.Combat
{
    /// <summary>What the hits of one instant did, for the simulation to apply and record.</summary>
    public sealed class HitResolution
    {
        public SimTime Time { get; }

        /// <summary>The hits that landed, in the order they were given.</summary>
        public IReadOnlyList<LandedHit> Landed { get; }

        /// <summary>Sides whose attack was cancelled at this instant (interrupted, or the dummy died): their weapons stop.</summary>
        public IReadOnlyList<Side> Interrupted { get; }

        public IReadOnlyList<Side> Died { get; }

        /// <summary>Domain events to record, all at <see cref="Time"/>: hits and stuns, then interrupts, then deaths.</summary>
        public IReadOnlyList<MatchEvent> Events { get; }

        public HitResolution(SimTime time, IReadOnlyList<LandedHit> landed, IReadOnlyList<Side> interrupted, IReadOnlyList<Side> died,
            IReadOnlyList<MatchEvent> events)
        {
            Time = time;
            Landed = Guard.NotNull(landed, nameof(landed));
            Interrupted = Guard.NotNull(interrupted, nameof(interrupted));
            Died = Guard.NotNull(died, nameof(died));
            Events = Guard.NotNull(events, nameof(events));
        }
    }
}
