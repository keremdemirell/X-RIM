using System;
using System.Collections.Generic;
using XRim.Core;
using XRim.Networking;
using XRim.Rules.Planning;

namespace XRim.Tests.PlayMode.App
{
    /// <summary>
    /// Plays one side from a script: each planning phase it sends the script's commands for that turn, then Ready, unless
    /// <see cref="HoldReady"/> says to wait: then the test sends it with <see cref="ReleaseReady"/>.
    /// </summary>
    internal sealed class ScriptedPlanSource : IPlanSource
    {
        private readonly Func<PlanningWindow, IEnumerable<PlanningCommand>> _script;
        private IPlanningCommandSink _heldSink;

        public Side Side { get; }

        /// <summary>For which planning phases Ready waits for <see cref="ReleaseReady"/>; null never waits.</summary>
        public Func<PlanningWindow, bool> HoldReady { get; set; }

        /// <summary>True while a planning phase waits for <see cref="ReleaseReady"/>.</summary>
        public bool IsHoldingReady => _heldSink != null;

        /// <summary>Every command's result, in order.</summary>
        public List<CommandResult> Results { get; } = new List<CommandResult>();

        public ScriptedPlanSource(Side side, Func<PlanningWindow, IEnumerable<PlanningCommand>> script)
        {
            Side = side;
            _script = script;
        }

        public void OnPlanningStarted(PlanningWindow window, IPlanningCommandSink sink)
        {
            foreach (PlanningCommand command in _script(window))
            {
                Results.Add(sink.Send(command));
            }

            if (HoldReady != null && HoldReady(window))
            {
                _heldSink = sink;
                return;
            }

            Results.Add(sink.Send(new SetReadyCommand(true)));
        }

        /// <summary>Sends the Ready a planning phase was holding.</summary>
        public void ReleaseReady()
        {
            if (_heldSink == null) return;
            IPlanningCommandSink sink = _heldSink;
            _heldSink = null;
            Results.Add(sink.Send(new SetReadyCommand(true)));
        }

        public void OnPlanningEnded() => _heldSink = null;
    }
}
