namespace XRim.Simulation.Execution
{
    /// <summary>Session 04's contact handler: no rules yet, every contact is recorded as a <see cref="ContactEvent"/>.</summary>
    public sealed class RecordContactsHandler : ITurnContactHandler
    {
        public void Handle(TurnContact contact, TurnContactContext context) => context.Record(new ContactEvent(contact));
    }
}
