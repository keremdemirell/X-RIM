namespace XRim.Input
{
    /// <summary>Where one finger's touch is in its life (the Input System's finer phases folded into four).</summary>
    public enum TouchSamplePhase
    {
        /// <summary>The finger just landed.</summary>
        Began = 0,

        /// <summary>The finger moved or rests.</summary>
        Moved = 1,

        /// <summary>The finger lifted.</summary>
        Ended = 2,

        /// <summary>The touch was taken away (for example by the system); it is not a deliberate release.</summary>
        Canceled = 3,
    }
}
