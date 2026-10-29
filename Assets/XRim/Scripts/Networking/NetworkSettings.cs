using System;
using XRim.Core.Gdd;

namespace XRim.Networking
{
    /// <summary>
    /// How an online match treats connections (Session 19): how long it waits for a dropped player and for both players' playback, and when
    /// a silent connection counts as lost. None of it changes how a turn resolves. The disconnect rules are GDD §3 TBDs on the designer's
    /// flagged defaults (2026-10-25).
    /// </summary>
    [Serializable]
    public sealed class NetworkSettings
    {
        /// <summary>
        /// A player missing when a planning phase would open is waited for this long, once per disconnect (the opponent sees "Opponent
        /// reconnecting…" counting down); then play goes on without them. They can rejoin any time until the match ends.
        /// </summary>
        [GddTbd("§3", "Reconnect window", Proposal = "Wait up to 15 s, once per disconnect, before the next planning phase; rejoin until the match ends (designer 2026-10-25)")]
        [Placeholder("Designer 2026-10-25: long enough for a signal drop, short enough that the opponent never sits in a long wait")]
        public float ReconnectWaitSeconds = 15f;

        /// <summary>
        /// A turn whose plans lock while the player is disconnected counts like any turn: with nothing set it is idle, so three in a row
        /// forfeit (GDD §3, Decided), ending an abandoned match in about a minute. Off: such a turn never counts as idle (the forfeit
        /// counter starts again).
        /// </summary>
        [GddTbd("§3", "Do missed turns count as idle turns?", Proposal = "Yes, what was set still runs (designer 2026-10-25)")]
        public bool MissedTurnsCountAsIdle = true;

        /// <summary>
        /// The next planning phase opens when both players finished playing the turn back, their instant replays included, but never later
        /// than this after the turn resolved (designer 2026-10-25, Q6). The offline authority waits 10 s for one player.
        /// </summary>
        [Placeholder("Designer 2026-10-25 (Q6): a KO with a long replay must fit; re-measure from real playback times")]
        public float PlaybackWaitSeconds = 20f;

        /// <summary>
        /// Where the phone finds the server: a numeric address or a name. The default is this computer, for the Editor and a Windows build on the
        /// same PC; a phone needs the PC's address on the home network (Windows: <c>ipconfig</c>, the IPv4 address), set in the debug
        /// overlay's Tuning tab (Network) or the Network asset.
        /// </summary>
        [Placeholder("Technical: the test server on this PC until matchmaking (Session 20)")]
        public string ServerHost = "127.0.0.1";

        /// <summary>The TCP port the server listens on and the phone connects to; the server's <c>-port</c> command-line option overrides it.</summary>
        [Placeholder("Technical: any free port; 7777 is the usual game-server default")]
        public int Port = 7777;

        /// <summary>
        /// A connection either end has heard nothing from for this long counts as dropped: the server stops expecting the player, the phone
        /// rejoins with its token.
        /// </summary>
        [Placeholder("No real phone connection measured yet")]
        public float ConnectionTimeoutSeconds = 10f;

        // The phone's side.

        /// <summary>How often the phone pings the server, measuring the round trip and keeping the connection heard.</summary>
        [Placeholder("Technical: often enough to follow a changing connection, rare enough to cost nothing")]
        public float PingIntervalSeconds = 1f;

        /// <summary>
        /// The phone's countdown and weapon lock-out run early by the measured round trip plus this much (designer 2026-10-25, Q5), so a
        /// command the phone allows always reaches the server before its own deadline, even when the connection wavers a little.
        /// </summary>
        [Placeholder("Designer 2026-10-25 (Q5): covers jitter beyond the measured round trip; re-measure on phones (PT7)")]
        public float LatencyMarginSeconds = 0.1f;

        /// <summary>After its connection drops, the phone tries again this often, rejoining its match with its token (GDD §3).</summary>
        [Placeholder("Technical: no real phone connection measured yet")]
        public float ReconnectRetrySeconds = 1f;
    }
}
