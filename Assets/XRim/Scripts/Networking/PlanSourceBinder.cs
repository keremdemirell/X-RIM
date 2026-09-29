using System;
using System.Collections.Generic;
using XRim.Core;
using XRim.Rules.Planning;

namespace XRim.Networking
{
    /// <summary>Connects plan sources to an authority, local or remote, for the length of a match.</summary>
    public sealed class PlanSourceBinder : IDisposable
    {
        private readonly ITurnAuthority _authority;
        private readonly IReadOnlyList<IPlanSource> _sources;

        public PlanSourceBinder(ITurnAuthority authority, IReadOnlyList<IPlanSource> sources)
        {
            _authority = Guard.NotNull(authority, nameof(authority));
            _sources = Guard.NotNull(sources, nameof(sources));
            _authority.PlanningStarted += OnPlanningStarted;
            _authority.PlanningLocked += OnPlanningLocked;
        }

        public void Dispose()
        {
            _authority.PlanningStarted -= OnPlanningStarted;
            _authority.PlanningLocked -= OnPlanningLocked;
        }

        private void OnPlanningStarted(PlanningWindow window)
        {
            foreach (IPlanSource source in _sources)
            {
                source.OnPlanningStarted(window, new SideSink(_authority, source.Side));
            }
        }

        private void OnPlanningLocked(int turnIndex)
        {
            foreach (IPlanSource source in _sources)
            {
                source.OnPlanningEnded();
            }
        }

        private sealed class SideSink : IPlanningCommandSink
        {
            private readonly ITurnAuthority _authority;
            private readonly Side _side;

            public SideSink(ITurnAuthority authority, Side side)
            {
                _authority = authority;
                _side = side;
            }

            public CommandResult Send(PlanningCommand command) => _authority.Send(_side, command);
        }
    }
}
