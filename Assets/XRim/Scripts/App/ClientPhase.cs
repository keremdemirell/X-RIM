namespace XRim.App
{
    /// <summary>What this device's screen is doing. Mirrors the authority; never decides outcomes.</summary>
    public enum ClientPhase
    {
        Idle = 0,
        Planning = 1,

        /// <summary>Plans are locked; waiting for the authority's TurnResult (instant when local).</summary>
        WaitingForAuthority = 2,
        Playback = 3,
        MatchOver = 4,
    }
}
