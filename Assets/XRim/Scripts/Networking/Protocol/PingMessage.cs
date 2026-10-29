namespace XRim.Networking.Protocol
{
    /// <summary>The client measures the round trip to the server; the server answers at once with <see cref="PongMessage"/>.</summary>
    public sealed class PingMessage : NetMessage
    {
        public override MessageKind Kind => MessageKind.Ping;

        public int PingId { get; }

        /// <summary>When the client sent it, on the client's own clock (only echoed back).</summary>
        public double ClientSentSeconds { get; }

        public PingMessage(int pingId, double clientSentSeconds)
        {
            PingId = pingId;
            ClientSentSeconds = clientSentSeconds;
        }
    }
}
