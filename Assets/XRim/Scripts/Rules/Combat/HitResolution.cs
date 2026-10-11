using System.Collections.Generic;
using XRim.Core;
using XRim.Rules.Events;
using XRim.Rules.Limbs;

namespace XRim.Rules.Combat
{
    /// <summary>What the hits of one instant did, for the simulation to apply and record.</summary>
    public sealed class HitResolution
    {
        public SimTime Time { get; }

        /// <summary>The hits that landed, in the order they were given.</summary>
        public IReadOnlyList<LandedHit> Landed { get; }

        /// <summary>
        /// Sides whose attack was cancelled at this instant (interrupted, or the dummy died): their weapons stop. A side whose weapon
        /// dropped is in <see cref="Severed"/> instead.
        /// </summary>
        public IReadOnlyList<Side> Interrupted { get; }

        public IReadOnlyList<Side> Died { get; }

        /// <summary>The limbs these hits severed (GDD §12), in the order of the hits: break each joint, drop what each lost arm held.</summary>
        public IReadOnlyList<SeverOutcome> Severed { get; }

        /// <summary>
        /// Domain events to record, all at <see cref="Time"/>: each hit followed by its stun or sever, then interrupts, then deaths.
        /// </summary>
        public IReadOnlyList<MatchEvent> Events { get; }

        public HitResolution(SimTime time, IReadOnlyList<LandedHit> landed, IReadOnlyList<Side> interrupted, IReadOnlyList<Side> died,
            IReadOnlyList<SeverOutcome> severed, IReadOnlyList<MatchEvent> events)
        {
            Time = time;
            Landed = Guard.NotNull(landed, nameof(landed));
            Interrupted = Guard.NotNull(interrupted, nameof(interrupted));
            Died = Guard.NotNull(died, nameof(died));
            Severed = Guard.NotNull(severed, nameof(severed));
            Events = Guard.NotNull(events, nameof(events));
        }
    }
}
