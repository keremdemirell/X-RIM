namespace XRim.Networking.Protocol
{
    /// <summary>
    /// One message of the online protocol (Session 19). Messages are plain immutable data; <see cref="ProtocolCodec"/> turns them into
    /// bytes and back. What a message may carry is decided by who sends it: a client sends only its own inputs (raw strokes and commands,
    /// GDD §18), and during planning only public state crosses to the opponent (GDD §3, §6, §8).
    /// </summary>
    public abstract class NetMessage
    {
        public abstract MessageKind Kind { get; }
    }
}
