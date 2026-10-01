using System;
using XRim.Core;
using XRim.Rules;
using XRim.Rules.Match;
using XRim.Rules.Planning;
using XRim.Rules.Settings;
using XRim.Simulation;
using XRim.Simulation.Execution;
using XRim.Simulation.Physics;
using XRim.Simulation.Recording;

namespace XRim.Networking
{
    /// <summary>
    /// In-process authority: runs <see cref="MatchStateMachine"/> and an <see cref="ITurnSimulator"/> on this device.
    /// Used for the offline prototype, bot matches, hot-seat debugging and tests. A server would host the same
    /// class headless.
    /// <list type="bullet">
    /// <item><see cref="Send"/> only records the command and raises events; the simulator runs inside <see cref="Tick"/>,
    /// at most one turn per call, so a caller's input handler never pays for a simulation and a bot-vs-bot loop can
    /// stop between turns.</item>
    /// <item>Events in order for a turn: <see cref="PlanningStarted"/>, <see cref="PublicStateChanged"/> as weapons and
    /// Ready change, <see cref="PlanningLocked"/> (both Ready, or the timers ended), <see cref="TurnResolved"/>, then
    /// <see cref="MatchEnded"/> or the next <see cref="PlanningStarted"/>.</item>
    /// <item>The <see cref="TurnResult"/> raised carries the final board the rules state machine ended on (idle counters,
    /// sudden death), so a client plays back exactly what the next planning phase starts from.</item>
    /// <item>The next planning timer starts after <see cref="PlaybackHoldSettings"/> says playback is over, so the replay
    /// never eats planning time (ARCHITECTURE §5). Clients report it with <see cref="NotifyPlaybackFinished"/>.</item>
    /// </list>
    /// </summary>
    public sealed class LocalTurnAuthority : ITurnAuthority
    {
        public event Action<MatchStartInfo> MatchStarted;
        public event Action<PlanningWindow> PlanningStarted;
        public event Action<Side, PublicPlanningState> PublicStateChanged;
        public event Action<int> PlanningLocked;
        public event Action<TurnResult> TurnResolved;
        public event Action<MatchOutcome> MatchEnded;

        private readonly PlaybackHoldSettings _hold;
        private readonly IWeaponTipLocator _weaponTips;
        private readonly PoseSnapshot _initialPose;

        private BoardSnapshot _board;
        private PerSide<PublicPlanningState> _publicStates;
        private bool _planningLockedAnnounced;
        private bool _holding;
        private int _heldTurnIndex;
        private double _holdUntilSeconds;

        private MatchStateMachine Machine { get; }
        private ITurnSimulator Simulator { get; }
        private RulesSettings Rules { get; }
        private SimulationSettings SimulationSettings { get; }
        private IClock Clock { get; }

        public LocalTurnAuthority(MatchSetup setup, RulesSettings rules, SimulationSettings simulation,
            RulePolicies policies, ITurnSimulator simulator, IClock clock, LocalTurnAuthorityOptions options = null)
        {
            Rules = Guard.NotNull(rules, nameof(rules));
            SimulationSettings = Guard.NotNull(simulation, nameof(simulation));
            Machine = new MatchStateMachine(setup, rules, policies, clock);
            Simulator = Guard.NotNull(simulator, nameof(simulator));
            Clock = clock;

            options = options ?? new LocalTurnAuthorityOptions();
            _hold = options.PlaybackHold ?? new PlaybackHoldSettings();
            _weaponTips = options.WeaponTips ?? new GuardStanceWeaponTipLocator(rules, simulation);
            _initialPose = options.InitialPose ?? new PoseSnapshot();
        }

        public MatchPhase Phase => Machine.Phase;

        /// <summary>Set once the match is over.</summary>
        public MatchOutcome Outcome => Machine.Outcome;

        public void Start()
        {
            Machine.Start(); // throws on a second call or a broken loadout
            _board = new BoardSnapshot(Machine.State.Clone(), _initialPose.Clone());
            RaiseMatchStarted(new MatchStartInfo(Machine.Setup, _board.Clone()));
            BeginNextPlanning();
        }

        /// <summary>
        /// Advances the match: locks planning when the timers end, runs the simulator for a locked turn (one turn per
        /// call) and starts the next planning phase once the playback hold is over. Call once per frame.
        /// </summary>
        public void Tick()
        {
            Machine.Tick();

            switch (Machine.Phase)
            {
                case MatchPhase.Locked:
                    AnnouncePlanningLocked();
                    ResolveTurn();
                    break;
                case MatchPhase.TurnStart:
                case MatchPhase.SuddenDeathSetup:
                    if (_holding && Clock.NowSeconds >= _holdUntilSeconds) BeginNextPlanning();
                    break;
            }
        }

        public CommandResult Send(Side side, PlanningCommand command)
        {
            CommandResult result = Machine.Apply(side, command);
            if (result.Accepted) PublishPublicStateChange(side);

            AnnouncePlanningLocked();
            return result;
        }

        public void NotifyPlaybackFinished(int turnIndex)
        {
            if (!_holding || _hold.Mode != PlaybackHoldMode.ClientReport || turnIndex != _heldTurnIndex) return;

            // Released on the next Tick, so a client's playback callback never re-enters the authority.
            _holdUntilSeconds = Clock.NowSeconds;
        }

        private void BeginNextPlanning()
        {
            _holding = false;
            _planningLockedAnnounced = false;

            PerSide<IWeaponTipSource> tips = PerSide<IWeaponTipSource>.Create(side => _weaponTips.ForSide(_board, side));
            Machine.BeginPlanning(tips);
            _publicStates = PerSide<PublicPlanningState>.Create(side => Machine.PublicStateOf(side));

            RaisePlanningStarted(new PlanningWindow(Machine.State.TurnIndex, Machine.PlanningDeadlineSeconds,
                Machine.Constraints, Machine.State.IsSuddenDeath, _board.Clone(), tips));
        }

        private void ResolveTurn()
        {
            PerSide<TurnPlan> plans = Machine.BeginExecution();
            var input = new TurnInput(new BoardSnapshot(Machine.State.Clone(), _board.Pose.Clone()), plans, Rules, SimulationSettings);
            TurnResult simulated = Simulator.Simulate(input);

            Machine.CompleteExecution(simulated.Report);
            _board = new BoardSnapshot(Machine.State.Clone(), simulated.FinalBoard.Pose.Clone());
            var result = new TurnResult(simulated.TurnIndex, simulated.Timeline, _board.Clone(), simulated.Report);
            RaiseTurnResolved(result);

            if (Machine.Phase == MatchPhase.MatchOver)
            {
                RaiseMatchEnded(Machine.Outcome);
                return;
            }

            HoldForPlayback(result);
        }

        private void HoldForPlayback(TurnResult result)
        {
            double now = Clock.NowSeconds;
            switch (_hold.Mode)
            {
                case PlaybackHoldMode.TimelineDuration:
                    _holdUntilSeconds = now + result.Timeline.Duration.Seconds + _hold.ExtraSeconds;
                    break;
                case PlaybackHoldMode.ClientReport:
                    _holdUntilSeconds = now + _hold.ClientReportTimeoutSeconds;
                    break;
                default:
                    BeginNextPlanning();
                    return;
            }

            _holding = true;
            _heldTurnIndex = result.TurnIndex;
        }

        private void PublishPublicStateChange(Side side)
        {
            PublicPlanningState state = Machine.PublicStateOf(side);
            if (state == _publicStates[side]) return;

            _publicStates[side] = state;
            RaisePublicStateChanged(side, state);
        }

        private void AnnouncePlanningLocked()
        {
            if (Machine.Phase != MatchPhase.Locked || _planningLockedAnnounced) return;

            _planningLockedAnnounced = true;
            RaisePlanningLocked(Machine.State.TurnIndex);
        }

        private void RaiseMatchStarted(MatchStartInfo info) => MatchStarted?.Invoke(info);
        private void RaisePlanningStarted(PlanningWindow window) => PlanningStarted?.Invoke(window);
        private void RaisePublicStateChanged(Side side, PublicPlanningState state) => PublicStateChanged?.Invoke(side, state);
        private void RaisePlanningLocked(int turnIndex) => PlanningLocked?.Invoke(turnIndex);
        private void RaiseTurnResolved(TurnResult result) => TurnResolved?.Invoke(result);
        private void RaiseMatchEnded(MatchOutcome outcome) => MatchEnded?.Invoke(outcome);
    }
}
