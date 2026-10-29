using System;

namespace XRim.Networking.Transport
{
    /// <summary>Where a server takes new connections (Session 19), polled from its own loop.</summary>
    public interface INetListener : IDisposable
    {
        /// <summary>The next connection a player opened; false when none is waiting.</summary>
        bool TryAccept(out INetConnection connection);
    }
}
