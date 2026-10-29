using System;

namespace XRim.Networking.Protocol
{
    /// <summary>A received message was refused: it says why (<see cref="Error"/>) and what was wrong.</summary>
    public sealed class ProtocolException : Exception
    {
        public ProtocolError Error { get; }

        public ProtocolException(ProtocolError error, string message, Exception inner = null) : base(message, inner)
        {
            Error = error;
        }
    }
}
