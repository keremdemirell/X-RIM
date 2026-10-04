namespace XRim.Presentation.UI
{
    /// <summary>What the Ready button offers (GDD §3: Ready, and whether it can be cancelled is D9, a flagged rule).</summary>
    public enum ReadyButtonState
    {
        /// <summary>Not Ready yet: pressing it says "I'm done".</summary>
        Ready = 0,

        /// <summary>Ready, and the rules still allow taking it back to edit the plan.</summary>
        Cancel = 1,

        /// <summary>Ready, and it can no longer be cancelled (the lock-out has started).</summary>
        LockedIn = 2,
    }
}
