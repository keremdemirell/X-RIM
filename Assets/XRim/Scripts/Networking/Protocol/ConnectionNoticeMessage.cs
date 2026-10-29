using XRim.Core;

namespace XRim.Networking.Protocol
{
    /// <summary>
    /// A player's connection changed (GDD §3): both players are told, so the opponent sees "Opponent reconnecting…" with the time the server
    /// still waits (designer 2026-10-25, Q4).
    /// </summary>
    public sealed class ConnectionNoticeMessage : NetMessage
    {
        public override MessageKind Kind => MessageKind.ConnectionNotice;

        /// <summary>Whose connection it is.</summary>
        public Side Side { get; }

        public ConnectionStatus Status { get; }

        /// <summary>
        /// How long the server holds the next planning phase for the player from now; 0 when it is not holding it (the match simply goes on,
        /// or the wait for this disconnect is used up).
        /// </summary>
        public float WaitSecondsLeft { get; }

        public ConnectionNoticeMessage(Side side, ConnectionStatus status, float waitSecondsLeft)
        {
            Side = side;
            Status = status;
            WaitSecondsLeft = waitSecondsLeft;
        }
    }
}
