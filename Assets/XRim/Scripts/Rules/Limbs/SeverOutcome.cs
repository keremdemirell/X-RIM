using System.Collections.Generic;
using XRim.Core;
using XRim.Rules.Events;

namespace XRim.Rules.Limbs
{
    /// <summary>What one sever did to the rules state, for the simulation to apply (break the joint, drop the held item) and record.</summary>
    public sealed class SeverOutcome
    {
        public SimTime Time { get; }
        public Side Side { get; }
        public BodyPart Part { get; }

        /// <summary>The arm that held the weapon, shield or club was severed, so the held item drops (GDD §12).</summary>
        public bool DropsHeldItem { get; }

        /// <summary>What dropped; empty when nothing did.</summary>
        public WeaponId DroppedItem { get; }

        /// <summary>The severed limb the dummy wielded as a club, now back on the floor; null when it held none.</summary>
        public SeveredLimbRef? ReleasedClub { get; }

        /// <summary>The limb's <see cref="LimbSeveredEvent"/>, then a <see cref="WeaponDroppedEvent"/> when the held item drops.</summary>
        public IReadOnlyList<MatchEvent> Events { get; }

        public SeverOutcome(SimTime time, Side side, BodyPart part, bool dropsHeldItem, WeaponId droppedItem, SeveredLimbRef? releasedClub,
            IReadOnlyList<MatchEvent> events)
        {
            Time = time;
            Side = side;
            Part = part;
            DropsHeldItem = dropsHeldItem;
            DroppedItem = droppedItem;
            ReleasedClub = releasedClub;
            Events = Guard.NotNull(events, nameof(events));
        }

        public SeveredLimbRef Limb => new SeveredLimbRef(Side, Part);
    }
}
