using XRim.Core;

namespace XRim.Networking
{
    /// <summary>
    /// Supplies one side's planning inputs: a human on this device (touch), a bot, a hot-seat debug player
    /// entering both sides, or a recording for replays and tests.
    /// </summary>
    public interface IPlanSource
    {
        Side Side { get; }

        void OnPlanningStarted(PlanningWindow window, IPlanningCommandSink sink);

        void OnPlanningEnded();
    }
}
