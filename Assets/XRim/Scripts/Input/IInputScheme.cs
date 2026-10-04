using System.Collections.Generic;
using XRim.Core;
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
        /// <summary>Planning starts for the layout's own side: touches become commands to <paramref name="sink"/> until <see cref="End"/>.</summary>
        void Begin(PlanningWindow window, ScreenLayout layout, IPlanningCommandSink sink);

        /// <summary>Reads this frame's touches. Called once per frame during planning.</summary>
        void Tick();

        /// <summary>A different layout from now on (the window changed size, handedness changed). Fingers already down are dropped.</summary>
        void SetLayout(ScreenLayout layout);

        /// <summary>Forgets every finger that is down and the stroke in progress, without sending anything.</summary>
        void CancelGestures();

        /// <summary>
        /// The stroke being drawn right now, in the torso frame, for the live preview; empty when no finger is drawing. It is
        /// sent as a plan only when the finger lifts.
        /// </summary>
        IReadOnlyList<Vec2> ActiveStroke { get; }

        void End();
    }
}
