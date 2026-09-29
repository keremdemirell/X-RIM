using XRim.Rules.Planning;

namespace XRim.Networking
{
    /// <summary>Where a plan source sends its commands; bound to one side.</summary>
    public interface IPlanningCommandSink
    {
        CommandResult Send(PlanningCommand command);
    }
}
