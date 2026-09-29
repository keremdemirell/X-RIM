using XRim.Core.Gdd;
using XRim.Networking;

namespace XRim.Input
{
    /// <summary>
    /// A whole control scheme that turns touches into planning commands. The dual-zone scheme is the current design;
    /// the designer expects it to change, so it can be replaced as a unit.
    /// </summary>
    [GddTbd("§4–5", "Overall feel of the dual-zone scheme", Proposal = "Validate in prototype")]
    public interface IInputScheme
    {
        void Begin(ScreenLayout layout, IPlanningCommandSink sink);

        /// <summary>Reads this frame's touches. Called once per frame during planning.</summary>
        void Tick();

        void End();
    }
}
