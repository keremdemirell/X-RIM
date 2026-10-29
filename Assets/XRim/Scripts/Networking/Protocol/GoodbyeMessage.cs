namespace XRim.Networking.Protocol
{
    /// <summary>The server closes the connection, saying why; <see cref="Detail"/> is for logs and developers, never shown as game text.</summary>
    public sealed class GoodbyeMessage : NetMessage
    {
        public override MessageKind Kind => MessageKind.Goodbye;

        public GoodbyeReason Reason { get; }

        public string Detail { get; }

        public GoodbyeMessage(GoodbyeReason reason, string detail = "")
        {
            Reason = reason;
            Detail = detail ?? string.Empty;
        }
    }
}
