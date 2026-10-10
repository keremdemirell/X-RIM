using System;
using System.Collections.Generic;
using XRim.Core;
using XRim.Networking;
using XRim.Rules.Planning;

namespace XRim.Tests.PlayMode.App
{
    /// <summary>Plays one side from a script: each planning phase it sends the script's commands for that turn, then Ready.</summary>
    internal sealed class ScriptedPlanSource : IPlanSource
    {
        private readonly Func<PlanningWindow, IEnumerable<PlanningCommand>> _script;

        public Side Side { get; }

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

            Results.Add(sink.Send(new SetReadyCommand(true)));
        }

        public void OnPlanningEnded()
        {
        }
    }
}
