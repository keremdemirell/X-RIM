using XRim.Core;
using XRim.Rules.Events;

namespace XRim.Simulation.Execution
{
    /// <summary>
    /// A raw physics contact recorded in the timeline, before any rule decided what it means. Session 04 records every
    /// contact this way; once the hit, clash and block rules exist (Sessions 06 and 07) they emit domain events instead.
    /// Debug tools list these.
    /// </summary>
    public sealed class ContactEvent : MatchEvent
    {
        public TurnContact Contact { get; }

        public ContactEvent(TurnContact contact) : base(Guard.NotNull(contact, nameof(contact)).Time)
        {
            Contact = contact;
        }
    }
}
