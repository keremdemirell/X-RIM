using System.Collections.Generic;

namespace XRim.Simulation.Execution
{
    /// <summary>No rules: every contact is recorded as a <see cref="ContactEvent"/>. The feel report uses it to measure raw physics.</summary>
    public sealed class RecordContactsHandler : ITurnContactHandler
    {
        public void BeginTurn(TurnContactContext context)
        {
        }

        public void Handle(IReadOnlyList<TurnContact> contacts, TurnContactContext context)
        {
            foreach (TurnContact contact in contacts)
            {
                context.Record(new ContactEvent(contact));
            }
        }
    }
}
