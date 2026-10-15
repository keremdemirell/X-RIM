namespace XRim.Config
{
    /// <summary>How the match camera frames the duel (GDD §2: camera behaviour is TBD, a fixed side view is assumed).</summary>
    public enum CameraFramingMode
    {
        /// <summary>The fixed view the match scene was built with: the whole arena, edge to edge.</summary>
        WholeArena = 0,

        /// <summary>
        /// A closer fixed side view centred between the two fighters (designer 2026-10-11, F3). It is set when a turn starts, settles on
        /// the next framing after playback, and never moves during planning or while bodies move.
        /// </summary>
        Duel = 1,
    }
}
