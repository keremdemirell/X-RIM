using System;
using XRim.Core;
using XRim.Rules.Planning;
using XRim.Rules.Settings;

namespace XRim.Rules.Match
{
    /// <summary>
    /// The authoritative match flow (see <see cref="MatchPhase"/>). Pure C#, driven by <see cref="Tick"/> with an
    /// injected <see cref="IClock"/>, so tests control time and a server can host it unchanged.
    /// Whoever owns this object is the authority; clients only mirror its events.
    /// </summary>
    public sealed class MatchStateMachine
    {
        public MatchPhase Phase { get; private set; } = MatchPhase.MatchSetup;
        public MatchSetup Setup { get; }
        public MatchState State { get; }
        public RulesSettings Settings { get; }
        public PerSide<PlanningSession> Sessions { get; private set; }

        private RulePolicies Policies { get; }
        private IClock Clock { get; }

        public MatchStateMachine(MatchSetup setup, RulesSettings settings, RulePolicies policies, IClock clock)
        {
            Setup = Guard.NotNull(setup, nameof(setup));
            Settings = Guard.NotNull(settings, nameof(settings));
            Policies = Guard.NotNull(policies, nameof(policies));
            Clock = Guard.NotNull(clock, nameof(clock));
            State = MatchState.CreateInitial(setup, settings);
        }

        /// <summary>Leaves MatchSetup and starts turn 1.</summary>
        public void Start()
        {
            // Placeholder: architecture setup only. Gameplay implementation comes later.
            throw new NotImplementedException("MatchStateMachine.Start is not implemented yet.");
        }

        /// <summary>Advances time-based transitions, e.g. Planning → Locked when both are Ready or the deadline passes.</summary>
        public void Tick()
        {
            // Placeholder: architecture setup only. Gameplay implementation comes later.
            throw new NotImplementedException("MatchStateMachine.Tick is not implemented yet.");
        }

        public CommandResult Apply(Side side, PlanningCommand command)
        {
            // Placeholder: architecture setup only. Gameplay implementation comes later.
            throw new NotImplementedException("MatchStateMachine.Apply is not implemented yet.");
        }

        /// <summary>Executing → Resolving: applies a simulated turn and runs the end-condition check.</summary>
        public EndCheckResult CompleteExecution(ExecutionReport report)
        {
            // Placeholder: architecture setup only. Gameplay implementation comes later.
            throw new NotImplementedException("MatchStateMachine.CompleteExecution is not implemented yet.");
        }
    }
}
