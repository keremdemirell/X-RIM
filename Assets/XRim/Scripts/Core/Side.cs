namespace XRim.Core
{
    /// <summary>
    /// Canonical arena side. Left and Right are simulation facts; which side a player sees
    /// their own dummy on is a presentation concern (handedness mirroring, GDD §4).
    /// </summary>
    public enum Side
    {
        Left = 0,
        Right = 1,
    }
}
