namespace XRim.Networking.Protocol
{
    /// <summary>
    /// Every message of the online protocol (Session 19), as the byte after the version in its header. Kinds 1–19 travel from a client to
    /// the server, 20 and up from the server to a client (<see cref="MessageKinds.IsSentByClient"/>): a server never accepts a server
    /// kind from a client, so a client can never send a turn's result (GDD §18: clients never send results).
    /// </summary>
    public enum MessageKind : byte
    {
        // Client → server.

        /// <summary>A player joins (or rejoins with its token): handedness and the signature moves brought.</summary>
        ClientHello = 1,

        /// <summary>The player's blind loadout pick (GDD §6): held by the server until both have picked.</summary>
        LoadoutPick = 2,

        /// <summary>One planning input as the player made it: a raw stroke, never a built path (GDD §18).</summary>
        PlanningCommand = 3,

        /// <summary>The client finished playing a turn back (its replay included), so the next planning phase can open.</summary>
        PlaybackFinished = 4,

        /// <summary>Measures the round trip, so the phone's countdown can run early by the delay.</summary>
        Ping = 5,

        // Server → client.

        /// <summary>An opponent was found: which side this player plays, its reconnect token and the server's settings.</summary>
        MatchFound = 20,

        /// <summary>Both loadouts revealed (GDD §6) and the starting board.</summary>
        MatchStarted = 21,

        /// <summary>A planning phase opens: the board, the limits, this player's own signature options and the time left.</summary>
        PlanningStarted = 22,

        /// <summary>A side's public planning state changed: weapon, Ready, a charge only when shown (GDD §3, §6, §8).</summary>
        PublicState = 23,

        /// <summary>The server's verdict on one of this player's own commands.</summary>
        CommandAnswer = 24,

        /// <summary>This player's own plan as the server holds it (after a refusal, or on a reconnect).</summary>
        PlanState = 25,

        /// <summary>Planning is over for the turn: both Ready or the timers ended.</summary>
        PlanningLocked = 26,

        /// <summary>The turn's whole result, the same for both players.</summary>
        TurnResolved = 27,

        MatchEnded = 28,

        /// <summary>A player dropped, came back or was given up on (GDD §3 disconnects).</summary>
        ConnectionNotice = 29,

        Pong = 30,

        /// <summary>The server ends the connection, with the reason (a different protocol version, a refused message, the match is over).</summary>
        Goodbye = 31,
    }
}
