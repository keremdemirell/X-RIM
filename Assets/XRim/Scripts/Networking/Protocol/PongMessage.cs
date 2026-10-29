namespace XRim.Networking.Protocol
{
    /// <summary>The server's answer to a <see cref="PingMessage"/>: the client's own send time back, and the server's clock when it answered.</summary>
    public sealed class PongMessage : NetMessage
    {
        public override MessageKind Kind => MessageKind.Pong;

        public int PingId { get; }

        public double ClientSentSeconds { get; }

        public double ServerNowSeconds { get; }

        public PongMessage(int pingId, double clientSentSeconds, double serverNowSeconds)
        {
            PingId = pingId;
            ClientSentSeconds = clientSentSeconds;
            ServerNowSeconds = serverNowSeconds;
        }
    }
}
