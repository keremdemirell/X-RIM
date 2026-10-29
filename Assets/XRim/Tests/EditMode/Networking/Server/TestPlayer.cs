using System;
using System.Collections.Generic;
using System.Linq;
using XRim.Core;
using XRim.Networking.Protocol;
using XRim.Networking.Transport;
using XRim.Rules;
using XRim.Rules.Planning;

namespace XRim.Tests.EditMode.Networking.Server
{
    /// <summary>A player as the server sees one: a connection that sends real protocol messages and keeps everything it received, decoded.</summary>
    internal sealed class TestPlayer
    {
        private INetConnection _connection;
        private int _sequence;

        public TestPlayer(INetConnection connection)
        {
            _connection = connection;
        }

        /// <summary>Every message received, oldest first (call <see cref="Read"/> to collect new ones).</summary>
        public List<NetMessage> Inbox { get; } = new List<NetMessage>();

        public Side Side { get; private set; }
        public string Token { get; private set; } = string.Empty;

        /// <summary>The turn of the last planning phase this player was told about.</summary>
        public int CurrentTurn { get; private set; }

        public bool IsOpen => _connection.IsOpen;

        public void Send(NetMessage message) => _connection.Send(ProtocolCodec.Encode(message));

        public void SendBytes(byte[] bytes) => _connection.Send(bytes);

        public void Hello(Handedness handedness = Handedness.Right, IReadOnlyList<SignatureMoveId> signatures = null, string token = "") =>
            Send(new ClientHelloMessage(handedness, signatures ?? new[] { SignatureMoveIds.FiveFingerExplodingTouch }, token));

        public void Pick(params WeaponId[] weapons) =>
            Send(new LoadoutPickMessage(weapons.Length > 0 ? weapons : new[] { WeaponIds.Rapier, WeaponIds.Mace, WeaponIds.Shield }));

        /// <summary>Sends a planning command for the current turn; returns its number.</summary>
        public int Command(PlanningCommand command, int? turnIndex = null)
        {
            int sequence = ++_sequence;
            Send(new PlanningCommandMessage(sequence, turnIndex ?? CurrentTurn, command));
            return sequence;
        }

        public void ReportPlayback() => Send(new PlaybackFinishedMessage(LastOrDefault<TurnResolvedMessage>().Result.TurnIndex));

        /// <summary>Cuts the connection from this end, as a phone losing its signal would.</summary>
        public void Drop() => _connection.Close();

        /// <summary>A new connection for this player (after a drop), which says hello with its token.</summary>
        public void Rejoin(INetConnection connection)
        {
            _connection = connection;
            Hello(token: Token);
        }

        /// <summary>Collects and decodes everything that arrived.</summary>
        public void Read()
        {
            while (_connection.TryReceive(out byte[] bytes))
            {
                NetMessage message = ProtocolCodec.Decode(bytes);
                Inbox.Add(message);
                switch (message)
                {
                    case MatchFoundMessage found:
                        Side = found.YourSide;
                        Token = found.ReconnectToken;
                        break;
                    case PlanningStartedMessage planning:
                        CurrentTurn = planning.TurnIndex;
                        break;
                }
            }
        }

        public List<T> All<T>() where T : NetMessage => Inbox.OfType<T>().ToList();

        public T LastOrDefault<T>() where T : NetMessage => Inbox.OfType<T>().LastOrDefault();

        /// <summary>Everything received since this many messages had arrived.</summary>
        public List<NetMessage> Since(int count) => Inbox.Skip(count).ToList();

        public T Last<T>() where T : NetMessage =>
            LastOrDefault<T>() ?? throw new InvalidOperationException($"No {typeof(T).Name} received; got {string.Join(", ", Inbox.Select(m => m.Kind))}.");
    }
}
