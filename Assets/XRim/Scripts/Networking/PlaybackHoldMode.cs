namespace XRim.Networking
{
    /// <summary>
    /// How long a <see cref="LocalTurnAuthority"/> waits after a turn resolves before the next planning timer starts.
    /// The replay of the turn must not eat planning time (ARCHITECTURE §5), so the authority holds the next planning
    /// phase until playback is expected to be over.
    /// </summary>
    public enum PlaybackHoldMode
    {
        /// <summary>No wait: planning starts at once. Headless matches, bots and tests.</summary>
        None = 0,

        /// <summary>Wait for the recorded timeline's duration plus <see cref="PlaybackHoldSettings.ExtraSeconds"/>. What a server would do.</summary>
        TimelineDuration = 1,

        /// <summary>
        /// Wait until the client calls <see cref="ITurnAuthority.NotifyPlaybackFinished"/>, but no longer than
        /// <see cref="PlaybackHoldSettings.ClientReportTimeoutSeconds"/>, so a dead client cannot stall the match.
        /// </summary>
        ClientReport = 2,
    }
}
