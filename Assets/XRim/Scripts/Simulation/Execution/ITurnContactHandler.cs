namespace XRim.Simulation.Execution
{
    /// <summary>
    /// What happens when two bodies touch during a turn: the seam between physics and the rules (ARCHITECTURE §2,
    /// "Simulation detects, Rules decide, Simulation applies"). Contacts arrive one physics step at a time, each step's in
    /// <see cref="TurnContactOrder"/>. Session 04 only records them (<see cref="RecordContactsHandler"/>); Sessions 06 and 07
    /// put the hit, priority, interrupt, clash and shield rules here.
    /// </summary>
    public interface ITurnContactHandler
    {
        void Handle(TurnContact contact, TurnContactContext context);
    }
}
