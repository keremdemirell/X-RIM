using XRim.Presentation.Feel;

namespace XRim.App
{
    /// <summary>
    /// One played turn's time on screen against its plan (Session 13; the debug Playback tab lists the last few): what the feel layer
    /// planned (the turn, its slow motion, its freezes) and what playback really took. The difference is the camera settling at the end,
    /// plus frame rounding.
    /// </summary>
    public readonly struct PlayedTurnPacing
    {
        public PlayedTurnPacing(int turnIndex, PacingBreakdown planned, float onScreenSeconds)
        {
            TurnIndex = turnIndex;
            Planned = planned;
            OnScreenSeconds = onScreenSeconds;
        }

        /// <summary>0-based, as the match counts turns.</summary>
        public int TurnIndex { get; }

        public PacingBreakdown Planned { get; }

        /// <summary>What playback really took, in real seconds.</summary>
        public float OnScreenSeconds { get; }

        /// <summary>What playback took beyond the plan: mostly the camera settling, plus frame rounding.</summary>
        public float UnplannedSeconds => OnScreenSeconds - Planned.PlannedSeconds;
    }
}
