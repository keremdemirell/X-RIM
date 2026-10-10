namespace XRim.Presentation.Playback
{
    /// <summary>
    /// Bends how fast a recorded turn plays: hitstop now, slow motion later (Session 13). The turn was resolved before playback,
    /// so pacing changes only what the screen shows and when, never what happened (ARCHITECTURE §1).
    /// </summary>
    public interface IPlaybackPacer
    {
        /// <summary>True while an effect holds playback; the turn does not finish until it lets go.</summary>
        bool IsHolding { get; }

        /// <summary>How far the timeline moves for this frame's real time (0 while frozen).</summary>
        float Pace(float realDeltaSeconds);

        /// <summary>A new play starts: drop whatever the last one left.</summary>
        void Reset();
    }
}
