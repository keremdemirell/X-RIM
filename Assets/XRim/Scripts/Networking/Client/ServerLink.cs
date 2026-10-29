using System;
using System.Collections.Generic;
using XRim.Core;
using XRim.Networking.Protocol;
using XRim.Networking.Transport;

namespace XRim.Networking.Client
{
    /// <summary>
    /// The phone's link to the server (Session 19), polled from its frame: it says hello, reads what arrives, pings every
    /// <see cref="NetworkSettings.PingIntervalSeconds"/> to measure the round trip (<see cref="RoundTripSeconds"/>), and when the connection
    /// drops or falls silent it opens a new one and rejoins its match with the token the server gave it (GDD §3). A goodbye from the server
    /// (the match is over, a version mismatch, a refusal) ends it for good, as does a drop before any token was given.
    /// </summary>
    public sealed class ServerLink
    {
        /// <summary>How much of each new round-trip measurement moves the estimate (technical smoothing).</summary>
        private const double RoundTripSmoothingFraction = 0.25;

        private readonly Func<INetConnection> _connect;
        private readonly IClock _clock;
        private readonly NetworkSettings _network;

        private INetConnection _connection;
        private ClientHelloMessage _hello;
        private double _lastHeardSeconds;
        private double _lastPingSeconds;
        private double? _retryAtSeconds;
        private int _nextPingId;

        /// <param name="connect">Opens a new connection to the server (TCP on a phone, the in-process loopback in tests); called again to rejoin.</param>
        public ServerLink(Func<INetConnection> connect, IClock clock, NetworkSettings network = null)
        {
            _connect = Guard.NotNull(connect, nameof(connect));
            _clock = Guard.NotNull(clock, nameof(clock));
            _network = network ?? new NetworkSettings();
        }

        /// <summary>The link is up (a connection is open and the link has not ended).</summary>
        public bool IsConnected => !IsEnded && _connection != null && _connection.IsOpen;

        /// <summary>The link ended for good; <see cref="EndReason"/> says why.</summary>
        public bool IsEnded { get; private set; }

        public GoodbyeReason EndReason { get; private set; }

        /// <summary>The server's words when it ended the link; empty when the phone ended it.</summary>
        public string EndDetail { get; private set; } = string.Empty;

        /// <summary>The token to rejoin the match with; empty until an opponent is found.</summary>
        public string Token { get; private set; } = string.Empty;

        /// <summary>The measured round trip to the server, smoothed; 0 until the first answer to a ping.</summary>
        public double RoundTripSeconds { get; private set; }

        /// <summary>The link went up (a rejoin) or down (a drop).</summary>
        public event Action<bool> ConnectionChanged;

        /// <summary>The link ended for good.</summary>
        public event Action<GoodbyeReason> Ended;

        /// <summary>Connects and says hello. Called once; later connections are rejoins with the token.</summary>
        public void Open(ClientHelloMessage hello)
        {
            if (_hello != null) throw new InvalidOperationException("The link is already open.");
            _hello = Guard.NotNull(hello, nameof(hello));
            Connect();
        }

        public void Send(NetMessage message)
        {
            Guard.NotNull(message, nameof(message));
            if (IsConnected) _connection.Send(ProtocolCodec.Encode(message));
        }

        /// <summary>Reads what arrived (decoded, oldest first) into <paramref name="received"/>, pings, and notices a drop or rejoins.</summary>
        public void Tick(List<NetMessage> received)
        {
            Guard.NotNull(received, nameof(received));
            if (IsEnded) return;
            double now = _clock.NowSeconds;

            if (_connection == null)
            {
                if (_retryAtSeconds.HasValue && now >= _retryAtSeconds.Value) Connect();
                return;
            }

            while (_connection.TryReceive(out byte[] bytes))
            {
                _lastHeardSeconds = now;
                if (!TryDecode(bytes, out NetMessage message)) return;
                switch (message)
                {
                    case MatchFoundMessage found:
                        Token = found.ReconnectToken;
                        received.Add(message);
                        break;
                    case PongMessage pong:
                        Measure(now - pong.ClientSentSeconds);
                        break;
                    case GoodbyeMessage goodbye:
                        End(goodbye.Reason, goodbye.Detail);
                        return;
                    default:
                        received.Add(message);
                        break;
                }
            }

            if (!_connection.IsOpen || now - _lastHeardSeconds > _network.ConnectionTimeoutSeconds) Lose();
            else if (now - _lastPingSeconds >= _network.PingIntervalSeconds) Ping(now);
        }

        /// <summary>The player leaves: the link ends for good and nothing reconnects.</summary>
        public void Close()
        {
            if (IsEnded) return;
            End(GoodbyeReason.ConnectionLost, "The player left.");
        }

        private void Connect()
        {
            _retryAtSeconds = null;
            double now = _clock.NowSeconds;
            try
            {
                _connection = _connect();
            }
            catch (Exception error) when (!(error is OutOfMemoryException))
            {
                // No network, or the server's name cannot be found: try again later, as after a drop.
                _connection = null;
                ScheduleRetryOrEnd($"Could not connect: {error.Message}");
                return;
            }

            _lastHeardSeconds = now;
            ClientHelloMessage hello = Token.Length == 0 ? _hello : new ClientHelloMessage(_hello.Handedness, _hello.Signatures, Token);
            _connection.Send(ProtocolCodec.Encode(hello));
            Ping(now);
            if (Token.Length > 0) ConnectionChanged?.Invoke(true);
        }

        private void Lose()
        {
            _connection.Close();
            _connection = null;
            ConnectionChanged?.Invoke(false);
            ScheduleRetryOrEnd("The connection was lost before an opponent was found.");
        }

        private void ScheduleRetryOrEnd(string detail)
        {
            if (Token.Length == 0)
            {
                End(GoodbyeReason.ConnectionLost, detail);
                return;
            }

            _retryAtSeconds = _clock.NowSeconds + _network.ReconnectRetrySeconds;
        }

        private void Ping(double now)
        {
            _lastPingSeconds = now;
            _connection.Send(ProtocolCodec.Encode(new PingMessage(_nextPingId++, now)));
        }

        private void Measure(double sampleSeconds)
        {
            if (sampleSeconds < 0.0) return;
            RoundTripSeconds = RoundTripSeconds <= 0.0 ? sampleSeconds : RoundTripSeconds + RoundTripSmoothingFraction * (sampleSeconds - RoundTripSeconds);
        }

        private bool TryDecode(byte[] bytes, out NetMessage message)
        {
            try
            {
                message = ProtocolCodec.Decode(bytes);
                return true;
            }
            catch (ProtocolException error)
            {
                message = null;
                End(error.Error == ProtocolError.VersionMismatch ? GoodbyeReason.VersionMismatch : GoodbyeReason.ConnectionLost, error.Message);
                return false;
            }
        }

        private void End(GoodbyeReason reason, string detail)
        {
            IsEnded = true;
            EndReason = reason;
            EndDetail = detail ?? string.Empty;
            _connection?.Close();
            _connection = null;
            _retryAtSeconds = null;
            Ended?.Invoke(reason);
        }
    }
}
