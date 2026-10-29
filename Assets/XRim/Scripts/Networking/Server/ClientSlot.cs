using System.Collections.Generic;
using XRim.Core;
using XRim.Networking.Protocol;
using XRim.Networking.Transport;
using XRim.Rules;

namespace XRim.Networking.Server
{
    /// <summary>One player's place in a match: their side, the token they rejoin with, their connection (none while dropped) and what they told the server.</summary>
    internal sealed class ClientSlot
    {
        public ClientSlot(Side side, string token, INetConnection connection, ClientHelloMessage hello, double nowSeconds)
        {
            Side = side;
            Token = token;
            Connection = connection;
            Handedness = hello.Handedness;
            Signatures = new List<SignatureMoveId>(hello.Signatures);
            LastHeardSeconds = nowSeconds;
        }

        public Side Side { get; }
        public string Token { get; }
        public Handedness Handedness { get; }
        public IReadOnlyList<SignatureMoveId> Signatures { get; }

        /// <summary>Null while the player is disconnected.</summary>
        public INetConnection Connection { get; set; }

        public bool IsConnected => Connection != null && Connection.IsOpen;

        /// <summary>When the server last received anything from the player (a phone pings every second).</summary>
        public double LastHeardSeconds { get; set; }

        /// <summary>When the player was dropped; null while connected.</summary>
        public double? DroppedAtSeconds { get; set; }

        /// <summary>When the server began holding the next planning phase for this player; null when it is not waiting.</summary>
        public double? WaitStartedSeconds { get; set; }

        /// <summary>The wait for this disconnect is used up: the match goes on without the player until they return.</summary>
        public bool WaitUsed { get; set; }

        /// <summary>The player finished playing back the turn the server is holding for.</summary>
        public bool ReportedPlayback { get; set; }

        public void Send(NetMessage message)
        {
            if (IsConnected) Connection.Send(ProtocolCodec.Encode(message));
        }
    }
}
