namespace XRim.Networking.Protocol
{
    /// <summary>Why the server closes a connection.</summary>
    public enum GoodbyeReason
    {
        /// <summary>The client speaks another version of the protocol: one of the two builds is out of date.</summary>
        VersionMismatch = 0,

        /// <summary>The client sent something the protocol refuses (a damaged message, or a kind only the server sends, such as a result).</summary>
        ProtocolViolation = 1,

        /// <summary>The reconnect token names no match this player can rejoin (it ended, or the token is wrong).</summary>
        ReconnectRefused = 2,

        /// <summary>The match is over.</summary>
        MatchOver = 3,

        /// <summary>The server is stopping.</summary>
        ServerStopping = 4,

        /// <summary>
        /// The phone's own verdict, never sent by a server: its connection dropped before it had a token to rejoin with (before an opponent
        /// was found), or the server's messages could not be read.
        /// </summary>
        ConnectionLost = 5,

        /// <summary>The server heard nothing from the device for too long (no hello, or silent while waiting for an opponent).</summary>
        TimedOut = 6,
    }
}
