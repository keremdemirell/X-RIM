namespace XRim.Networking.Protocol
{
    /// <summary>What happened to a player's connection (GDD §3 disconnects, TBD; designer 2026-10-25: flagged defaults).</summary>
    public enum ConnectionStatus
    {
        /// <summary>The connection dropped; the match goes on, and the server may wait a little before the next planning phase.</summary>
        Dropped = 0,

        /// <summary>The player came back and is playing again.</summary>
        Reconnected = 1,

        /// <summary>The server stopped waiting: the match carries on without the player, whose turns are idle until they return.</summary>
        StoppedWaiting = 2,
    }
}
