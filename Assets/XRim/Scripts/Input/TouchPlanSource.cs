using XRim.Core;
using XRim.Networking;

namespace XRim.Input
{
    /// <summary>A human on this device, planning through an <see cref="IInputScheme"/>.</summary>
    public sealed class TouchPlanSource : IPlanSource
    {
        private readonly IInputScheme _scheme;
        private readonly ScreenLayout _layout;
        private bool _planning;

        public Side Side { get; }

        public TouchPlanSource(Side side, IInputScheme scheme, ScreenLayout layout)
        {
            Side = side;
            _scheme = Guard.NotNull(scheme, nameof(scheme));
            _layout = Guard.NotNull(layout, nameof(layout));
        }

        public void OnPlanningStarted(PlanningWindow window, IPlanningCommandSink sink)
        {
            _planning = true;
            _scheme.Begin(_layout, sink);
        }

        /// <summary>Call once per frame.</summary>
        public void Tick()
        {
            if (_planning) _scheme.Tick();
        }

        public void OnPlanningEnded()
        {
            _planning = false;
            _scheme.End();
        }
    }
}
