namespace XRim.Rules
{
    /// <summary>
    /// Stance swipe result (GDD §5). The concept is Decided; the exact move set is TBD for feel,
    /// so per-move behaviour lives in data (BodyMoveStats), not in switch statements.
    /// </summary>
    public enum BodyMove
    {
        /// <summary>Neutral: the dummy stays in place.</summary>
        None = 0,

        /// <summary>Swipe down: drops the centre of gravity.</summary>
        Crouch = 1,

        /// <summary>Swipe forward: extra reach and momentum, more exposed.</summary>
        Lunge = 2,

        /// <summary>Swipe backward: slips linear thrusts. Triggers the electric wall (§13).</summary>
        StepBack = 3,

        /// <summary>Swipe up: clears low sweeps.</summary>
        Jump = 4,
    }
}
