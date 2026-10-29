using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using XRim.Core;
using XRim.Networking.Protocol;
using XRim.Networking.Transport;

namespace XRim.Networking.Server
{
    /// <summary>
    /// The online server's heart (Session 19, GDD §18 server-authoritative), engine-free: it takes connections from an
    /// <see cref="INetListener"/>, greets each player, pairs the first two who are waiting into a <see cref="ServerMatch"/> (real matchmaking
    /// is Session 20), puts a returning player back in their match by their token, and ticks every match on the server's clock. Call
    /// <see cref="Tick"/> once per server frame; everything happens there, one thing after another, so the matches can share one simulator.
    /// <para>
    /// A connection must say hello first. Anything else, a message that cannot be read, or a protocol version other than this server's is
    /// answered with a goodbye that says why, and the connection is closed.
    /// </para>
    /// </summary>
    public sealed class MatchServer : IDisposable
    {
        private const int TokenBytes = 16;

        private readonly INetListener _listener;
        private readonly IClock _clock;
        private readonly ServerGameSetup _game;
        private readonly NetworkSettings _network;
        private readonly RandomNumberGenerator _tokens = RandomNumberGenerator.Create();
        private readonly List<Waiting> _greeting = new List<Waiting>();
        private readonly List<Waiting> _lobby = new List<Waiting>();
        private readonly List<ServerMatch> _matches = new List<ServerMatch>();
        private int _nextMatchId = 1;
        private bool _disposed;

        public MatchServer(INetListener listener, IClock clock, ServerGameSetup game, NetworkSettings network = null)
        {
            _listener = Guard.NotNull(listener, nameof(listener));
            _clock = Guard.NotNull(clock, nameof(clock));
            _game = Guard.NotNull(game, nameof(game));
            _network = network ?? new NetworkSettings();
        }

        /// <summary>One line for each thing worth a log (a match found, started, each turn's result size, a drop, a refusal).</summary>
        public event Action<string> Logged;

        /// <summary>Matches being played (or picking their loadouts).</summary>
        public int MatchCount => _matches.Count;

        /// <summary>Players who said hello and wait for an opponent.</summary>
        public int WaitingPlayers => _lobby.Count;

        public void Tick()
        {
            if (_disposed) return;
            AcceptNewConnections();
            GreetNewConnections();
            KeepLobby();
            PairWaitingPlayers();

            foreach (ServerMatch match in _matches.ToArray())
            {
                match.Tick();
                if (match.IsOver) _matches.Remove(match);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            foreach (ServerMatch match in _matches)
            {
                match.Close(GoodbyeReason.ServerStopping, "The server is stopping.");
            }

            foreach (Waiting waiting in _greeting.ToArray())
            {
                Refuse(waiting, GoodbyeReason.ServerStopping, "The server is stopping.");
            }

            foreach (Waiting waiting in _lobby.ToArray())
            {
                Refuse(waiting, GoodbyeReason.ServerStopping, "The server is stopping.");
            }

            _matches.Clear();
            _greeting.Clear();
            _lobby.Clear();
            _listener.Dispose();
            _tokens.Dispose();
        }

        private void AcceptNewConnections()
        {
            while (_listener.TryAccept(out INetConnection connection))
            {
                _greeting.Add(new Waiting(connection, _clock.NowSeconds));
            }
        }

        /// <summary>A new connection's first message must be a hello: a new player goes to the lobby, a returning one back to their match.</summary>
        private void GreetNewConnections()
        {
            foreach (Waiting waiting in _greeting.ToArray())
            {
                if (!waiting.Connection.TryReceive(out byte[] bytes))
                {
                    if (!waiting.Connection.IsOpen) _greeting.Remove(waiting);
                    else if (_clock.NowSeconds - waiting.SinceSeconds > _network.ConnectionTimeoutSeconds) Refuse(waiting, GoodbyeReason.TimedOut, "No hello.");
                    continue;
                }

                if (!TryDecode(waiting, bytes, out NetMessage message)) continue;
                if (!(message is ClientHelloMessage hello))
                {
                    Refuse(waiting, GoodbyeReason.ProtocolViolation, $"A connection must say hello first, not send {message.Kind}.");
                    continue;
                }

                _greeting.Remove(waiting);
                if (hello.ReconnectToken.Length == 0)
                {
                    waiting.Hello = hello;
                    waiting.SinceSeconds = _clock.NowSeconds;
                    _lobby.Add(waiting);
                    continue;
                }

                ServerMatch match = _matches.Find(candidate => candidate.HasToken(hello.ReconnectToken));
                if (match == null || !match.Rejoin(hello.ReconnectToken, waiting.Connection))
                    Refuse(waiting, GoodbyeReason.ReconnectRefused, "No match to rejoin with this token.");
            }
        }

        /// <summary>Players waiting for an opponent may only ping (every second); one who leaves or falls silent is forgotten.</summary>
        private void KeepLobby()
        {
            foreach (Waiting waiting in _lobby.ToArray())
            {
                while (waiting.Connection.IsOpen && waiting.Connection.TryReceive(out byte[] bytes))
                {
                    if (!TryDecode(waiting, bytes, out NetMessage message)) break;
                    if (message is PingMessage ping)
                    {
                        waiting.Connection.Send(ProtocolCodec.Encode(new PongMessage(ping.PingId, ping.ClientSentSeconds, _clock.NowSeconds)));
                        waiting.SinceSeconds = _clock.NowSeconds;
                        continue;
                    }

                    Refuse(waiting, GoodbyeReason.ProtocolViolation, $"{message.Kind} is not expected before an opponent is found.");
                    break;
                }

                if (!waiting.Connection.IsOpen) _lobby.Remove(waiting);
                else if (_clock.NowSeconds - waiting.SinceSeconds > _network.ConnectionTimeoutSeconds) Refuse(waiting, GoodbyeReason.TimedOut, "Silent while waiting.");
            }
        }

        private void PairWaitingPlayers()
        {
            while (_lobby.Count >= 2)
            {
                Waiting left = _lobby[0];
                Waiting right = _lobby[1];
                _lobby.RemoveRange(0, 2);
                double now = _clock.NowSeconds;
                var match = new ServerMatch(_nextMatchId++, _game, _network, _clock,
                    new ClientSlot(Side.Left, NewToken(), left.Connection, left.Hello, now),
                    new ClientSlot(Side.Right, NewToken(), right.Connection, right.Hello, now), Log);
                _matches.Add(match);
            }
        }

        private bool TryDecode(Waiting waiting, byte[] bytes, out NetMessage message)
        {
            try
            {
                message = ProtocolCodec.Decode(bytes);
            }
            catch (ProtocolException error)
            {
                message = null;
                Refuse(waiting, error.Error == ProtocolError.VersionMismatch ? GoodbyeReason.VersionMismatch : GoodbyeReason.ProtocolViolation, error.Message);
                return false;
            }

            if (MessageKinds.IsSentByClient(message.Kind)) return true;
            Refuse(waiting, GoodbyeReason.ProtocolViolation, $"A client cannot send {message.Kind}: only the server sends it.");
            message = null;
            return false;
        }

        private void Refuse(Waiting waiting, GoodbyeReason reason, string detail)
        {
            if (waiting.Connection.IsOpen) waiting.Connection.Send(ProtocolCodec.Encode(new GoodbyeMessage(reason, detail)));
            waiting.Connection.Close();
            _greeting.Remove(waiting);
            _lobby.Remove(waiting);
            Log($"connection refused ({reason}): {detail}");
        }

        private string NewToken()
        {
            var bytes = new byte[TokenBytes];
            _tokens.GetBytes(bytes);
            return BitConverter.ToString(bytes).Replace("-", string.Empty);
        }

        private void Log(string message) => Logged?.Invoke(message);

        /// <summary>A connection not yet in a match: still to say hello, or waiting for an opponent.</summary>
        private sealed class Waiting
        {
            public Waiting(INetConnection connection, double sinceSeconds)
            {
                Connection = connection;
                SinceSeconds = sinceSeconds;
            }

            public INetConnection Connection { get; }
            public double SinceSeconds { get; set; }
            public ClientHelloMessage Hello { get; set; }
        }
    }
}
