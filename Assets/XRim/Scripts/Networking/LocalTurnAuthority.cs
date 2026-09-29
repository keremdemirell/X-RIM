using System;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Match;
using XRim.Rules.Planning;
using XRim.Rules.Settings;
using XRim.Simulation;
using XRim.Simulation.Execution;
using XRim.Simulation.Recording;

namespace XRim.Networking
{
    /// <summary>
    /// In-process authority: runs <see cref="MatchStateMachine"/> and an <see cref="ITurnSimulator"/> on this device.
    /// Used for the offline prototype, bot matches, hot-seat debugging and tests. A server would host the same
    /// class headless.
    /// </summary>
    public sealed class LocalTurnAuthority : ITurnAuthority
    {
        public event Action<MatchStartInfo> MatchStarted;
        public event Action<PlanningWindow> PlanningStarted;
        public event Action<Side, PublicPlanningState> PublicStateChanged;
        public event Action<int> PlanningLocked;
        public event Action<TurnResult> TurnResolved;
        public event Action<MatchOutcome> MatchEnded;

        private MatchStateMachine Machine { get; }
        private ITurnSimulator Simulator { get; }
        private SimulationSettings SimulationSettings { get; }

        public LocalTurnAuthority(MatchSetup setup, RulesSettings rules, SimulationSettings simulation,
            RulePolicies policies, ITurnSimulator simulator, IClock clock)
        {
            Machine = new MatchStateMachine(setup, rules, policies, clock);
            Simulator = Guard.NotNull(simulator, nameof(simulator));
            SimulationSettings = Guard.NotNull(simulation, nameof(simulation));
        }

        public MatchPhase Phase => Machine.Phase;

        public void Start()
        {
            // Placeholder: architecture setup only. Gameplay implementation comes later.
            throw new NotImplementedException("LocalTurnAuthority.Start is not implemented yet.");
        }

        public void Tick()
        {
            // Placeholder: architecture setup only. Gameplay implementation comes later.
            throw new NotImplementedException("LocalTurnAuthority.Tick is not implemented yet.");
        }

        public CommandResult Send(Side side, PlanningCommand command)
        {
            // Placeholder: architecture setup only. Gameplay implementation comes later.
            throw new NotImplementedException("LocalTurnAuthority.Send is not implemented yet.");
        }

        private void RaiseMatchStarted(MatchStartInfo info) => MatchStarted?.Invoke(info);
        private void RaisePlanningStarted(PlanningWindow window) => PlanningStarted?.Invoke(window);
        private void RaisePublicStateChanged(Side side, PublicPlanningState state) => PublicStateChanged?.Invoke(side, state);
        private void RaisePlanningLocked(int turnIndex) => PlanningLocked?.Invoke(turnIndex);
        private void RaiseTurnResolved(TurnResult result) => TurnResolved?.Invoke(result);
        private void RaiseMatchEnded(MatchOutcome outcome) => MatchEnded?.Invoke(outcome);
    }
}
