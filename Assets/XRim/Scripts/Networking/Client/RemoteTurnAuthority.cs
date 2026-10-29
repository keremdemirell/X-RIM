using System;
using System.Collections.Generic;
using XRim.Core;
using XRim.Networking.Protocol;
using XRim.Rules;
using XRim.Rules.Match;
using XRim.Rules.Paths;
using XRim.Rules.Planning;
using XRim.Rules.Settings;
using XRim.Rules.Signature;
using XRim.Simulation;
using XRim.Simulation.Recording;

namespace XRim.Networking.Client
{
    /// <summary>
    /// The phone's side of an online match (Session 19, GDD §18 server-authoritative): an <see cref="ITurnAuthority"/> whose decisions come
    /// from the server, so <c>ClientMatchFlow</c>, the HUD, the plan sources and playback work exactly as offline. The phone never
    /// simulates a turn: it plays back the server's result.
    /// <list type="bullet">
    /// <item>Planning: each window arrives with the time left, which becomes a deadline on this phone's clock, early by the measured round
    /// trip plus <see cref="NetworkSettings.LatencyMarginSeconds"/> (designer 2026-10-25, Q5), so a command the phone allows always reaches
    /// the server in time. The weapon tips come from the exact board (the server's own computation), this player's signature options from
    /// the server; the opponent's never arrive.</item>
    /// <item><see cref="Send"/> judges a command with a copy of this player's planning session (the same rules, the same limits) for
    /// instant feedback, and forwards only what it accepts, as the raw command. The server's verdict is the truth: when it refuses what the
    /// copy accepted, it sends the plan it holds and the copy is rebuilt from it (<see cref="IConnectionStatus.CommandRefused"/>).</item>
    /// <item>Everything the server sends is raised as the same events, in the same order, as <see cref="LocalTurnAuthority"/> raises them;
    /// connection news goes through <see cref="IConnectionStatus"/>. A dropped link rejoins by itself (<see cref="ServerLink"/>).</item>
    /// </list>
    /// </summary>
    public sealed class RemoteTurnAuthority : ITurnAuthority, IConnectionStatus
    {
        /// <summary>
        /// A rebuilt copy of the plan is replayed as if made long before any lock-out, since the server already accepted every part of it
        /// (technical: the copy's audit times are never checked).
        /// </summary>
        private const double ReplaySeconds = double.MinValue;

        private readonly ServerLink _link;
        private readonly MatchSetup _setup;
        private readonly RulesSettings _rules;
        private readonly RulePolicies _policies;
        private readonly IClock _clock;
        private readonly NetworkSettings _network;
        private readonly IWeaponTipLocator _tips;
        private readonly Queue<NetMessage> _pending;
        private readonly List<NetMessage> _received = new List<NetMessage>();
        private readonly PerSide<PublicPlanningState?> _publicStates = new PerSide<PublicPlanningState?>(null, null);

        private BoardSnapshot _board;
        private PlanningStartedMessage _planning;
        private PlanningWindow _window;
        private PlanningSession _mirror;
        private double _ownDeadlineSeconds;
        private int _sequence;
        private bool _started;

        public event Action<MatchStartInfo> MatchStarted;
        public event Action<PlanningWindow> PlanningStarted;
        public event Action<Side, PublicPlanningState> PublicStateChanged;
        public event Action<int> PlanningLocked;
        public event Action<TurnResult> TurnResolved;
        public event Action<MatchOutcome> MatchEnded;

        public event Action<bool> ConnectionChanged;
        public event Action<GoodbyeReason> Ended;
        public event Action<ConnectionNoticeMessage> OpponentConnectionChanged;
        public event Action<CommandRejection> CommandRefused;
        public event Action<BoardSnapshot> Resynchronized;
        public event Action OwnPlanRestored;

        /// <param name="started">Both loadouts and the starting board, as the server revealed them.</param>
        /// <param name="pending">What arrived after <paramref name="started"/> before this authority existed, handled first.</param>
        public RemoteTurnAuthority(ServerLink link, Side side, MatchStartedMessage started, IEnumerable<NetMessage> pending, RulesSettings rules,
            SimulationSettings simulation, RulePolicies policies, IClock clock, NetworkSettings network = null)
        {
            _link = Guard.NotNull(link, nameof(link));
            Side = side;
            Guard.NotNull(started, nameof(started));
            _setup = started.Setup;
            _board = started.InitialBoard;
            _pending = new Queue<NetMessage>(pending ?? Array.Empty<NetMessage>());
            _rules = Guard.NotNull(rules, nameof(rules));
            _policies = Guard.NotNull(policies, nameof(policies));
            _clock = Guard.NotNull(clock, nameof(clock));
            _network = network ?? new NetworkSettings();
            _tips = new PoseWeaponTipLocator(rules, Guard.NotNull(simulation, nameof(simulation)));
            _link.ConnectionChanged += OnConnectionChanged;
            _link.Ended += OnEnded;
        }

        /// <summary>The side this phone's player plays; commands for the other side are refused.</summary>
        public Side Side { get; }

        public MatchSetup Setup => _setup;

        public bool IsConnected => _link.IsConnected;

        /// <summary>The measured round trip to the server (the countdown runs early by it, Q5).</summary>
        public double RoundTripSeconds => _link.RoundTripSeconds;

        /// <summary>This player's plan as the phone's copy holds it; null outside planning.</summary>
        public TurnPlan OwnPlan => _mirror?.CurrentPlan;

        /// <summary>The raw stroke behind <see cref="OwnPlan"/>'s path; empty when none is drawn or a signature move is set.</summary>
        public WeaponPath OwnStroke =>
            _mirror != null && _mirror.SelectedSignature.IsEmpty && _mirror.CurrentPath != null ? _mirror.CurrentPath.Drawn : WeaponPath.Empty;

        public void Start()
        {
            if (_started) throw new InvalidOperationException("The match has already started.");
            _started = true;
            MatchStarted?.Invoke(new MatchStartInfo(_setup, _board.Clone()));
        }

        /// <summary>Pumps the link and raises what the server said, in order. Call once per frame.</summary>
        public void Tick()
        {
            if (!_started) return;
            while (_pending.Count > 0)
            {
                Handle(_pending.Dequeue());
            }

            _received.Clear();
            _link.Tick(_received);
            foreach (NetMessage message in _received)
            {
                Handle(message);
            }
        }

        public CommandResult Send(Side side, PlanningCommand command)
        {
            Guard.NotNull(command, nameof(command));
            if (side != Side || _mirror == null) return CommandResult.Reject(CommandRejection.NotInPlanningPhase);

            CommandResult result = _mirror.Apply(command, _clock.NowSeconds);
            if (!result.Accepted) return result;

            _link.Send(new PlanningCommandMessage(++_sequence, _window.TurnIndex, command));
            Publish(Side, _mirror.PublicState);
            return result;
        }

        public void NotifyPlaybackFinished(int turnIndex) => _link.Send(new PlaybackFinishedMessage(turnIndex));

        /// <summary>The player leaves the match: the link ends and nothing rejoins (the server treats them as gone, GDD §3).</summary>
        public void Leave()
        {
            _link.ConnectionChanged -= OnConnectionChanged;
            _link.Ended -= OnEnded;
            _link.Close();
        }

        private void Handle(NetMessage message)
        {
            switch (message)
            {
                case PlanningStartedMessage planning:
                    OpenPlanning(planning);
                    break;
                case PublicStateMessage state:
                    Publish(state.Side, state.State);
                    break;
                case CommandAnswerMessage answer:
                    if (!answer.Accepted) CommandRefused?.Invoke(answer.Rejection);
                    break;
                case PlanStateMessage plan:
                    RebuildMirror(plan);
                    break;
                case PlanningLockedMessage locked:
                    _mirror = null;
                    _publicStates.Left = null;
                    _publicStates.Right = null;
                    PlanningLocked?.Invoke(locked.TurnIndex);
                    break;
                case TurnResolvedMessage resolved:
                    _board = resolved.Result.FinalBoard;
                    TurnResolved?.Invoke(resolved.Result);
                    break;
                case MatchEndedMessage ended:
                    MatchEnded?.Invoke(ended.Outcome);
                    break;
                case MatchStartedMessage rejoined:
                    _board = rejoined.InitialBoard;
                    Resynchronized?.Invoke(_board.Clone());
                    break;
                case ConnectionNoticeMessage notice:
                    OpponentConnectionChanged?.Invoke(notice);
                    break;
            }
        }

        /// <summary>A planning window on this phone's clock, and a fresh copy of this player's session for instant feedback.</summary>
        private void OpenPlanning(PlanningStartedMessage planning)
        {
            double now = _clock.NowSeconds;
            double early = _link.RoundTripSeconds + _network.LatencyMarginSeconds;
            _planning = planning;
            _board = planning.Board;
            _ownDeadlineSeconds = now + planning.OwnRemainingSeconds - early;

            PerSide<IWeaponTipSource> tips = PerSide<IWeaponTipSource>.Create(side => _tips.ForSide(planning.Board, side));
            List<SignatureMoveStats> moves = SignatureCatalogue.Brought(_rules, planning.OwnSignatures.Moves);
            var own = new SignatureOptions(moves, planning.OwnSignatures.Charge, planning.OwnSignatures.CanUse);
            PerSide<SignatureOptions> signatures = PerSide<SignatureOptions>.Create(side => side == Side ? own : SignatureOptions.None);
            _window = new PlanningWindow(planning.TurnIndex, now + planning.RemainingSeconds - early, planning.Constraints, planning.IsSuddenDeath,
                planning.Board, tips, signatures, planning.PublicStates);
            _mirror = NewMirror();
            _publicStates.Left = planning.PublicStates.Left;
            _publicStates.Right = planning.PublicStates.Right;
            PlanningStarted?.Invoke(_window);
        }

        private PlanningSession NewMirror()
        {
            SignatureOptions own = _window.Signatures[Side];
            return new PlanningSession(Side, _planning.Board.State.Fighters[Side].CurrentWeapon, _setup.Fighters[Side].Loadout, _window.WeaponTips[Side],
                _planning.Constraints[Side], _ownDeadlineSeconds, _rules, _policies, own.Charge, own.Moves);
        }

        /// <summary>The copy starts again from the plan the server holds: weapon, body move, the stroke or the signature move, Ready.</summary>
        private void RebuildMirror(PlanStateMessage plan)
        {
            if (_window == null || _mirror == null || plan.TurnIndex != _window.TurnIndex) return;

            PlanningSession mirror = NewMirror();
            mirror.Apply(new SelectWeaponCommand(plan.Weapon), ReplaySeconds);
            if (plan.BodyMove != BodyMove.None) mirror.Apply(new SetBodyMoveCommand(plan.BodyMove), ReplaySeconds);
            if (!plan.Signature.IsEmpty) mirror.Apply(new UseSignatureCommand(plan.Signature), ReplaySeconds);
            else if (plan.Stroke.Count > 0) mirror.Apply(new SetPathCommand(new WeaponPath(plan.Stroke)), ReplaySeconds);
            if (plan.IsReady) mirror.Apply(new SetReadyCommand(true), ReplaySeconds);
            _mirror = mirror;
            Publish(Side, mirror.PublicState);
            OwnPlanRestored?.Invoke();
        }

        /// <summary>A side's public state, raised once per change (this player's own from the copy at once, then the server's echo).</summary>
        private void Publish(Side side, PublicPlanningState state)
        {
            if (_publicStates[side] == state) return;
            _publicStates[side] = state;
            PublicStateChanged?.Invoke(side, state);
        }

        private void OnConnectionChanged(bool connected) => ConnectionChanged?.Invoke(connected);

        private void OnEnded(GoodbyeReason reason) => Ended?.Invoke(reason);
    }
}
