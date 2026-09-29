namespace XRim.Rules
{
    /// <summary>
    /// Account-wide choice made at first launch (GDD §4, Decided). Sets the dummy's dominant hand,
    /// which matters for desperation (§12). Screen mirroring is derived from it in the Input layer.
    /// </summary>
    public enum Handedness
    {
        Right = 0,
        Left = 1,
    }
}
