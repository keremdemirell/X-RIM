using System;
using System.Collections.Generic;
using XRim.Core;
using XRim.Networking.Protocol;
using XRim.Networking.Transport;
using XRim.Rules;
using XRim.Rules.Match;
using XRim.Rules.Planning;
using XRim.Simulation.Recording;

namespace XRim.Networking.Server
{
    /// <summary>
    /// One online match on the server (Session 19, GDD §18 server-authoritative). It runs the same <see cref="LocalTurnAuthority"/> the
    /// offline game uses, on the server's clock, and stands between it and the two players:
    /// <list type="bullet">
    /// <item>The players' raw commands go to the authority as they arrive, so the server builds every path from the raw stroke, stamps
    /// every command with its own clock and validates the locked plans (ink, lock-out, Ready time; §18). Each command gets the server's
    /// verdict; a refused one also gets the player's plan as the server holds it, so the phone can put its copy right.</item>
    /// <item>What leaves goes through <see cref="ClientView"/>: during planning the opponent receives only public state (weapon, Ready, a
    /// charge only when shown), never a body move, a path or a chosen signature move (GDD §3, §6, §8).</item>
    /// <item>Before the match: the blind loadout pick (GDD §6, Session 14), held until both have picked, then both revealed. Ownership is the
    /// phone's own claim until the server keeps profiles (designer 2026-10-25, Q1).</item>
    /// <item>After a turn: the next planning phase opens once both players report their playback (replays included) or the wait runs out
    /// (Q6).</item>
    /// <item>Disconnects (GDD §3, TBD; <see cref="NetworkSettings"/>): the match goes on; when a planning phase would open while a player is
    /// gone, the server waits for them once per disconnect; turns locked without them count as idle unless switched off; a player who
    /// returns with their token gets the current board and phase. If both are gone too long, the match is abandoned without a result.</item>
    /// <item>A client that sends anything only the server sends (a turn's result, a planning window) or a message that cannot be read is told
    /// why and disconnected (GDD §18: clients never send results).</item>
    /// </list>
    /// </summary>
    internal sealed class ServerMatch
    {
        private readonly ServerGameSetup _game;
        private readonly NetworkSettings _network;
        private readonly IClock _clock;
        private readonly Action<string> _log;
        private readonly PerSide<ClientSlot> _slots;
        private readonly LoadoutSelection _picks;
        private readonly PerSide<bool> _missedThisTurn = new PerSide<bool>(false, false);

        private LocalTurnAuthority _authority;
        private MatchSetup _setup;
        private BoardSnapshot _board;
        private PlanningWindow _window;
        private int _heldTurn = -1;
        private bool _playbackReported;
        private double? _bothGoneSince;

        public ServerMatch(int id, ServerGameSetup game, NetworkSettings network, IClock clock, ClientSlot left, ClientSlot right, Action<string> log)
        {
            Id = id;
            _game = game;
            _network = network;
            _clock = clock;
            _log = log;
            _slots = new PerSide<ClientSlot>(left, right);
            _picks = new LoadoutSelection(game.Rules, new AllWeaponsOwned());

            foreach (ClientSlot slot in Slots)
            {
                slot.Send(new MatchFoundMessage(slot.Side, slot.Token, game.SettingsText));
            }

            Log("found: Left and Right are picking their loadouts");
        }

        public int Id { get; }

        /// <summary>The match ended, or was abandoned; the server can forget it.</summary>
        public bool IsOver { get; private set; }

        public bool HasToken(string token) => _slots.Left.Token == token || _slots.Right.Token == token;

        private IEnumerable<ClientSlot> Slots => new[] { _slots.Left, _slots.Right };

        private bool OpponentSeesCharge => _game.Rules.Signature.OpponentSeesCharged;

        private double Now => _clock.NowSeconds;

        public void Tick()
        {
            if (IsOver) return;
            foreach (ClientSlot slot in Slots)
            {
                Poll(slot);
            }

            if (IsOver) return;
            if (AbandonIfDeserted()) return;
            if (_authority == null) return;
            if (HoldForReturningPlayer()) return;
            _authority.Tick();
        }

        /// <summary>A player rejoins with their token (GDD §3): they get the match as it stands now. False when the token is not this match's.</summary>
        public bool Rejoin(string token, INetConnection connection)
        {
            ClientSlot slot = _slots.Left.Token == token ? _slots.Left : _slots.Right.Token == token ? _slots.Right : null;
            if (slot == null || IsOver) return false;

            if (slot.Connection != null && slot.Connection != connection) slot.Connection.Close();
            slot.Connection = connection;
            slot.LastHeardSeconds = Now;
            slot.DroppedAtSeconds = null;
            slot.WaitStartedSeconds = null;
            slot.WaitUsed = false;
            _bothGoneSince = null;
            slot.Send(new MatchFoundMessage(slot.Side, slot.Token, _game.SettingsText));
            if (_authority != null)
            {
                slot.Send(new MatchStartedMessage(ClientView.Setup(_setup, slot.Side), ClientView.Board(_board, slot.Side, OpponentSeesCharge)));
                if (_authority.Phase == MatchPhase.Planning && _window != null)
                {
                    SendPlanningStarted(slot);
                    SendPlanState(slot);
                }
                else
                {
                    slot.ReportedPlayback = true; // they missed the turn's playback: nothing to wait for
                    ReportPlaybackWhenEveryoneHas();
                }
            }

            _slots[slot.Side.Opponent()].Send(new ConnectionNoticeMessage(slot.Side, ConnectionStatus.Reconnected, 0f));
            Log($"{slot.Side} rejoined");
            return true;
        }

        /// <summary>The server is stopping: both players are told and disconnected.</summary>
        public void Close(GoodbyeReason reason, string detail)
        {
            foreach (ClientSlot slot in Slots)
            {
                slot.Send(new GoodbyeMessage(reason, detail));
                slot.Connection?.Close();
                slot.Connection = null;
            }

            IsOver = true;
        }

        // ----- receiving -----

        private void Poll(ClientSlot slot)
        {
            if (slot.Connection == null) return;
            while (slot.Connection != null && slot.Connection.TryReceive(out byte[] bytes))
            {
                slot.LastHeardSeconds = Now;
                NetMessage message;
                try
                {
                    message = ProtocolCodec.Decode(bytes);
                }
                catch (ProtocolException error)
                {
                    Refuse(slot, error.Error == ProtocolError.VersionMismatch ? GoodbyeReason.VersionMismatch : GoodbyeReason.ProtocolViolation,
                        error.Message);
                    return;
                }

                Handle(slot, message);
                if (IsOver) return;
            }

            if (slot.Connection == null) return;
            if (!slot.Connection.IsOpen) Drop(slot, "the connection closed");
            else if (Now - slot.LastHeardSeconds > _network.ConnectionTimeoutSeconds) Drop(slot, "nothing heard from the player");
        }

        private void Handle(ClientSlot slot, NetMessage message)
        {
            if (!MessageKinds.IsSentByClient(message.Kind))
            {
                Refuse(slot, GoodbyeReason.ProtocolViolation, $"A client cannot send {message.Kind}: only the server sends it.");
                return;
            }

            switch (message)
            {
                case PingMessage ping:
                    slot.Send(new PongMessage(ping.PingId, ping.ClientSentSeconds, Now));
                    break;
                case LoadoutPickMessage pick:
                    OnLoadoutPick(slot, pick);
                    break;
                case PlanningCommandMessage command:
                    OnCommand(slot, command);
                    break;
                case PlaybackFinishedMessage finished:
                    if (_authority != null && finished.TurnIndex == _heldTurn)
                    {
                        slot.ReportedPlayback = true;
                        ReportPlaybackWhenEveryoneHas();
                    }

                    break;
                default:
                    Refuse(slot, GoodbyeReason.ProtocolViolation, $"{message.Kind} is not expected during a match.");
                    break;
            }
        }

        private void OnLoadoutPick(ClientSlot slot, LoadoutPickMessage pick)
        {
            if (_authority != null || _picks.IsLockedIn(slot.Side)) return; // already picked (a rejoin can send it again)
            if (!_picks.TryLockIn(slot.Side, pick.Weapons, out IReadOnlyList<string> issues))
            {
                Refuse(slot, GoodbyeReason.ProtocolViolation, "The loadout pick is not valid: " + string.Join(" ", issues));
                return;
            }

            if (_picks.IsComplete) StartMatch();
        }

        private void OnCommand(ClientSlot slot, PlanningCommandMessage command)
        {
            bool planning = _authority != null && _authority.Phase == MatchPhase.Planning && _window != null && command.TurnIndex == _window.TurnIndex;
            if (!planning)
            {
                slot.Send(new CommandAnswerMessage(command.Sequence, CommandRejection.NotInPlanningPhase));
                return;
            }

            // The server's own clock stamps the command inside the authority (GDD §18); the raw stroke is built into a path there.
            CommandResult result = _authority.Send(slot.Side, command.Command);
            slot.Send(new CommandAnswerMessage(command.Sequence, result.Rejection));
            if (!result.Accepted && _authority.Phase == MatchPhase.Planning) SendPlanState(slot);
        }

        // ----- the match -----

        private void StartMatch()
        {
            var handedness = new PerSide<Handedness>(_slots.Left.Handedness, _slots.Right.Handedness);
            PerSide<FighterSetup> picked = _picks.CreateFighters(handedness);
            var fighters = PerSide<FighterSetup>.Create(side =>
                new FighterSetup(picked[side].Handedness, picked[side].Loadout, _slots[side].Signatures));
            _setup = new MatchSetup(fighters, (uint)_game.Seeds.NextInt(1, int.MaxValue));

            RulePolicies policies = _game.CreatePolicies();
            if (!_network.MissedTurnsCountAsIdle) policies.IdleTurn = new MissedTurnIdlePolicy(policies.IdleTurn, side => _missedThisTurn[side]);
            var options = new LocalTurnAuthorityOptions
            {
                PlaybackHold = new PlaybackHoldSettings { Mode = PlaybackHoldMode.ClientReport, ClientReportTimeoutSeconds = _network.PlaybackWaitSeconds },
            };
            _authority = new LocalTurnAuthority(_setup, _game.Rules, _game.Simulation, policies, _game.Simulator, _clock, options);
            _authority.MatchStarted += OnMatchStarted;
            _authority.PlanningStarted += OnPlanningStarted;
            _authority.PublicStateChanged += OnPublicStateChanged;
            _authority.PlanningLocked += OnPlanningLocked;
            _authority.TurnResolved += OnTurnResolved;
            _authority.MatchEnded += OnMatchEnded;
            Log("both loadouts locked in: the match starts");
            _authority.Start();
        }

        private void OnMatchStarted(MatchStartInfo info)
        {
            _board = info.InitialBoard;
            foreach (ClientSlot slot in Slots)
            {
                slot.Send(new MatchStartedMessage(ClientView.Setup(info.Setup, slot.Side), ClientView.Board(info.InitialBoard, slot.Side, OpponentSeesCharge)));
            }
        }

        private void OnPlanningStarted(PlanningWindow window)
        {
            _window = window;
            foreach (ClientSlot slot in Slots)
            {
                SendPlanningStarted(slot);
            }
        }

        private void OnPublicStateChanged(Side side, PublicPlanningState state)
        {
            foreach (ClientSlot slot in Slots)
            {
                slot.Send(new PublicStateMessage(side, state));
            }
        }

        private void OnPlanningLocked(int turnIndex)
        {
            foreach (ClientSlot slot in Slots)
            {
                _missedThisTurn[slot.Side] = !slot.IsConnected;
                slot.Send(new PlanningLockedMessage(turnIndex));
            }
        }

        private void OnTurnResolved(TurnResult result)
        {
            _board = result.FinalBoard;
            _window = null;
            _heldTurn = result.TurnIndex;
            _playbackReported = false;
            int bytes = 0;
            foreach (ClientSlot slot in Slots)
            {
                slot.ReportedPlayback = !slot.IsConnected;
                byte[] message = ProtocolCodec.Encode(new TurnResolvedMessage(ClientView.Result(result, slot.Side, OpponentSeesCharge)));
                bytes = message.Length;
                if (slot.IsConnected) slot.Connection.Send(message);
            }

            Log($"turn {result.TurnIndex + 1} resolved ({result.Timeline.Frames.Count} frames, {result.Timeline.Events.Count} events, {bytes} bytes sent)");
        }

        private void OnMatchEnded(MatchOutcome outcome)
        {
            foreach (ClientSlot slot in Slots)
            {
                slot.Send(new MatchEndedMessage(outcome));
            }

            Log($"over: {outcome.Winner} wins by {outcome.Reason} after {outcome.TurnCount} turns");
            Close(GoodbyeReason.MatchOver, "The match is over.");
        }

        private void SendPlanningStarted(ClientSlot slot)
        {
            PlanningSession own = _authority.SessionOf(slot.Side);
            double now = Now;
            var publicStates = PerSide<PublicPlanningState>.Create(side => _authority.SessionOf(side).PublicState);
            slot.Send(new PlanningStartedMessage(_window.TurnIndex, Math.Max(0.0, _window.DeadlineSeconds - now),
                Math.Max(0.0, own.DeadlineSeconds - now), _window.IsSuddenDeath, _window.Constraints,
                ClientView.Board(_window.Board, slot.Side, OpponentSeesCharge), SignatureOffer.Of(_window.Signatures[slot.Side]), publicStates));
        }

        /// <summary>The player's own plan as the server holds it: after a refusal, and when they rejoin mid-planning.</summary>
        private void SendPlanState(ClientSlot slot)
        {
            PlanningSession session = _authority.SessionOf(slot.Side);
            if (session == null) return;
            IReadOnlyList<Vec2> stroke = session.SelectedSignature.IsEmpty && session.CurrentPath != null
                ? session.CurrentPath.Drawn.Points
                : (IReadOnlyList<Vec2>)Array.Empty<Vec2>();
            TurnPlan plan = session.CurrentPlan;
            slot.Send(new PlanStateMessage(_window.TurnIndex, session.Weapon, plan.BodyMove, stroke, session.SelectedSignature, session.IsReady));
        }

        /// <summary>The next planning phase may open once every connected player has watched the turn (the authority's own wait still caps it).</summary>
        private void ReportPlaybackWhenEveryoneHas()
        {
            if (_playbackReported || _heldTurn < 0) return;
            foreach (ClientSlot slot in Slots)
            {
                if (slot.IsConnected && !slot.ReportedPlayback) return;
            }

            _playbackReported = true;
            _authority.NotifyPlaybackFinished(_heldTurn);
        }

        // ----- disconnects (GDD §3, TBD) -----

        private void Refuse(ClientSlot slot, GoodbyeReason reason, string detail)
        {
            slot.Send(new GoodbyeMessage(reason, detail));
            Log($"{slot.Side} refused ({reason}): {detail}");
            Drop(slot, "refused");
        }

        private void Drop(ClientSlot slot, string why)
        {
            slot.Connection?.Close();
            slot.Connection = null;
            slot.DroppedAtSeconds = Now;
            // No countdown yet: the match goes on, and the wait starts only when a planning phase would open without them.
            _slots[slot.Side.Opponent()].Send(new ConnectionNoticeMessage(slot.Side, ConnectionStatus.Dropped, 0f));
            Log($"{slot.Side} dropped: {why}");
            ReportPlaybackWhenEveryoneHas();
        }

        /// <summary>
        /// Between turns, with a player gone whose wait for this disconnect is not used up, the next planning phase is held (once per
        /// disconnect, up to <see cref="NetworkSettings.ReconnectWaitSeconds"/>). The authority's clock still runs, so its own playback wait
        /// is already over when it resumes.
        /// </summary>
        private bool HoldForReturningPlayer()
        {
            MatchPhase phase = _authority.Phase;
            if (phase != MatchPhase.TurnStart && phase != MatchPhase.SuddenDeathSetup) return false;

            bool holding = false;
            foreach (ClientSlot slot in Slots)
            {
                if (slot.IsConnected || slot.WaitUsed) continue;
                if (slot.WaitStartedSeconds == null)
                {
                    slot.WaitStartedSeconds = Now;
                    _slots[slot.Side.Opponent()].Send(new ConnectionNoticeMessage(slot.Side, ConnectionStatus.Dropped, _network.ReconnectWaitSeconds));
                }

                if (Now - slot.WaitStartedSeconds.Value < _network.ReconnectWaitSeconds)
                {
                    holding = true;
                    continue;
                }

                slot.WaitUsed = true;
                slot.WaitStartedSeconds = null;
                _slots[slot.Side.Opponent()].Send(new ConnectionNoticeMessage(slot.Side, ConnectionStatus.StoppedWaiting, 0f));
                Log($"stopped waiting for {slot.Side}: their turns are idle until they return");
            }

            return holding;
        }

        /// <summary>
        /// Nobody left to play for: both players gone longer than the reconnect wait, or one gone that long before the match started (the
        /// other is told). The match ends without a result.
        /// </summary>
        private bool AbandonIfDeserted()
        {
            bool bothGone = !_slots.Left.IsConnected && !_slots.Right.IsConnected;
            if (!bothGone) _bothGoneSince = null;
            else if (_bothGoneSince == null) _bothGoneSince = Now;

            if (bothGone && Now - _bothGoneSince.Value >= _network.ReconnectWaitSeconds)
            {
                Log("abandoned: both players gone");
                Close(GoodbyeReason.MatchOver, "Both players left.");
                return true;
            }

            if (_authority != null) return false;
            foreach (ClientSlot slot in Slots)
            {
                if (slot.IsConnected || slot.DroppedAtSeconds == null || Now - slot.DroppedAtSeconds.Value < _network.ReconnectWaitSeconds) continue;
                Log($"abandoned: {slot.Side} left before the match started");
                Close(GoodbyeReason.MatchOver, "Your opponent left before the match started.");
                return true;
            }

            return false;
        }

        private void Log(string message) => _log?.Invoke($"match {Id}: {message}");
    }
}
