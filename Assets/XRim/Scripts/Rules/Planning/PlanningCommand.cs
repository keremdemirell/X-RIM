namespace XRim.Rules.Planning
{
    /// <summary>
    /// One input intent sent to the authority during planning. Input only emits these; the rules
    /// (<see cref="PlanningSession"/>) decide whether each is allowed. The authority supplies the time
    /// of receipt from its own clock, so clients cannot fake timing (GDD §18).
    /// </summary>
    public abstract class PlanningCommand
    {
    }
}
