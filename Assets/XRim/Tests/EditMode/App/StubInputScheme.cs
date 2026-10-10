using System.Collections.Generic;
using XRim.Core;
using XRim.Input;
using XRim.Networking;

namespace XRim.Tests.EditMode.App
{
    /// <summary>An input scheme that reads no touches: tests drive a <see cref="TouchPlanSource"/> through its <c>Send</c>.</summary>
    internal sealed class StubInputScheme : IInputScheme
    {
        public IReadOnlyList<Vec2> ActiveStroke { get; } = new List<Vec2>();

        /// <summary>How often the gestures in progress were dropped (the source was switched off).</summary>
        public int CancelCount { get; private set; }

        public void Begin(PlanningWindow window, ScreenLayout layout, IPlanningCommandSink sink)
        {
        }

        public void Tick()
        {
        }

        public void SetLayout(ScreenLayout layout)
        {
        }

        public void CancelGestures() => CancelCount++;

        public void End()
        {
        }
    }
}
