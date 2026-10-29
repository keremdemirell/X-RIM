namespace XRim.Networking.Transport
{
    /// <summary>
    /// One end of a connection that carries whole messages (Session 19). A transport frames them, so a sent byte array arrives as one
    /// received byte array, in order, or not at all. Everything is polled from the caller's own loop (the server's frame, the phone's
    /// frame): nothing here raises events or runs on another thread. Messages that arrived before the connection closed can still be read.
    /// </summary>
    public interface INetConnection
    {
        /// <summary>False once either end closed it or the link failed.</summary>
        bool IsOpen { get; }

        /// <summary>Queues one whole message; does nothing once the connection is closed.</summary>
        void Send(byte[] message);

        /// <summary>The next whole message that arrived, oldest first; false when none is waiting.</summary>
        bool TryReceive(out byte[] message);

        /// <summary>Closes the connection; messages already queued are sent first where the transport can.</summary>
        void Close();
    }
}
