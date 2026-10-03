using XRim.Core;
using XRim.Rules;
using XRim.Rules.Events;
using XRim.Rules.Match;
using XRim.Rules.Settings;
using XRim.Simulation.Recording;

namespace XRim.Simulation.Execution
{
    /// <summary>
    /// What an <see cref="ITurnContactHandler"/> can read and change while a turn runs: the rules state, the settings and
    /// policies, each side's body move, the fighters (<see cref="Actions"/>) and the timeline.
    /// </summary>
    public sealed class TurnContactContext
    {
        private readonly TimelineRecorder _recorder;

        /// <summary>The rules state being resolved this turn. Its final value is the turn's resolved state.</summary>
        public MatchState State { get; }

        public RulesSettings Rules { get; }
        public RulePolicies Policies { get; }
        public SimulationSettings Simulation { get; }

        /// <summary>
        /// Each side's planned body move this turn (the neutral move's data for no move); null when the tuning has no data for
        /// the move. Its <see cref="BodyMoveStats.DamageBonusFraction"/> feeds the damage (D13, TBD §5; 0 by default).
        /// </summary>
        public PerSide<BodyMoveStats> BodyMoves { get; }

        public ITurnActions Actions { get; }

        /// <summary>When each side landed its first valid hit this turn, for the sudden-death rules (§14). The handler sets it.</summary>
        public PerSide<SimTime?> FirstValidHitTime { get; } = new PerSide<SimTime?>(null, null);

        public TurnContactContext(MatchState state, RulesSettings rules, RulePolicies policies, SimulationSettings simulation,
            PerSide<BodyMoveStats> bodyMoves, ITurnActions actions, TimelineRecorder recorder)
        {
            State = Guard.NotNull(state, nameof(state));
            Rules = Guard.NotNull(rules, nameof(rules));
            Policies = Guard.NotNull(policies, nameof(policies));
            Simulation = Guard.NotNull(simulation, nameof(simulation));
            BodyMoves = Guard.NotNull(bodyMoves, nameof(bodyMoves));
            Actions = Guard.NotNull(actions, nameof(actions));
            _recorder = Guard.NotNull(recorder, nameof(recorder));
        }

        /// <summary>Adds a time-stamped event to the turn's timeline (VFX, audio, replays and tests read it).</summary>
        public void Record(MatchEvent matchEvent) => _recorder.RecordEvent(matchEvent);
    }
}
