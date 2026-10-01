using System;
using System.Collections.Generic;
using XRim.Core;
using XRim.Networking;
using XRim.Rules.Match;
using XRim.Rules.Planning;
using XRim.Simulation.Recording;

namespace XRim.Tests.EditMode.Networking
{
    /// <summary>Records commands and lets a test raise authority events by hand.</summary>
    internal sealed class FakeTurnAuthority : ITurnAuthority
    {
        public event Action<MatchStartInfo> MatchStarted;
        public event Action<PlanningWindow> PlanningStarted;
        public event Action<Side, PublicPlanningState> PublicStateChanged;
        public event Action<int> PlanningLocked;
        public event Action<TurnResult> TurnResolved;
        public event Action<MatchOutcome> MatchEnded;

        public List<(Side Side, PlanningCommand Command)> Received { get; } = new List<(Side, PlanningCommand)>();
        public List<int> PlaybackFinishedTurns { get; } = new List<int>();

        public void Start() => MatchStarted?.Invoke(null);

        public void Tick()
        {
        }

        public CommandResult Send(Side side, PlanningCommand command)
        {
            Received.Add((side, command));
            return CommandResult.Ok;
        }

        public void NotifyPlaybackFinished(int turnIndex) => PlaybackFinishedTurns.Add(turnIndex);

        public void RaisePlanningStarted(PlanningWindow window) => PlanningStarted?.Invoke(window);

        public void RaisePlanningLocked(int turnIndex) => PlanningLocked?.Invoke(turnIndex);

        public void RaisePublicStateChanged(Side side, PublicPlanningState state) => PublicStateChanged?.Invoke(side, state);

        public void RaiseTurnResolved(TurnResult result) => TurnResolved?.Invoke(result);

        public void RaiseMatchEnded(MatchOutcome outcome) => MatchEnded?.Invoke(outcome);
    }
}
